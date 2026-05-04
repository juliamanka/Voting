# Voting Backend

Backend services for a voting system used to compare three implementation styles for the same functional domain:

- synchronous request processing,
- asynchronous event-driven processing,
- hybrid processing, where the main vote write happens immediately and secondary projection/audit work is delegated.

The solution is written in .NET 9 and uses ASP.NET Core, Entity Framework Core, SQL Server, MassTransit, RabbitMQ, SignalR, OpenTelemetry, Prometheus, Grafana, Serilog, FluentValidation, AutoMapper, and NBomber.

## Repository Layout

```text
Voting/
|-- Voting.sln
|-- Voting.Api.Common/
|   |-- Contracts/Monitoring/        Shared monitoring DTOs
|   |-- Middleware/                  Global exception handling middleware
|   |-- RequestTiming/               Per-request timing context
|   `-- ExceptionHandlingExtensions.cs
|-- src/
|   |-- Voting.Domain/               Domain entities, enums, repository contracts
|   |-- Voting.Application/          DTOs, validators, service interfaces, application services
|   |-- Voting.Infrastructure/       EF Core DbContext, migrations, repository implementations
|   |-- SynchronousVoting.Api/       Synchronous HTTP API
|   |-- AsynchronousVoting.Api/      Async HTTP API and SignalR projection notifications
|   |-- AsynchronousVoting.Worker/   Async queue consumers and projection processor
|   |-- HybridVoting.Api/            Hybrid HTTP API and SignalR projection notifications
|   |-- Hybrid.Worker/               Hybrid projection/audit worker
|   `-- Voting.LoadTests/            NBomber load test runner
|-- run_all.sh                       Automated architecture/scenario runner
|-- automate_tests.py                Python orchestration and resource collection helper
|-- verify_db_reset.py               Database reset verification helper
|-- collect_results.py               Test result aggregation helper
|-- nginx-lb.conf                    Load-balancer configuration for scaled tests
|-- nbomber-reports/                 Generated NBomber reports
`-- test_results/                    Generated automated experiment output
```

The monitoring stack lives next to this repository in `../monitoring`:

```text
monitoring/
|-- docker-compose.yml       Prometheus, Grafana, Grafana renderer
|-- prometheus.yml           Scrape config for APIs and workers
`-- sql_server_compose.yml   SQL Server container
```

## Solution Projects

| Project | Type | Responsibility |
| --- | --- | --- |
| `Voting.Domain` | Class library | Core entities such as polls, options, vote records, submissions, eligibility records, audit logs, and read-model projections. Also contains repository interfaces. |
| `Voting.Application` | Class library | Application layer: DTOs, validation, voting workflow services, projection/audit service, poll/result readers, AutoMapper profile, DI registration. |
| `Voting.Infrastructure` | Class library | EF Core SQL Server persistence, `VotingDbContext`, migrations, repository implementations, MassTransit transactional outbox entities. |
| `Voting.Api.Common` | Class library | Shared API concerns: global error handling, request timing, monitoring contracts. |
| `SynchronousVoting.Api` | ASP.NET Core Web API | Fully synchronous vote path. The request validates, writes the vote, updates projection/audit state, and returns after processing. |
| `AsynchronousVoting.Api` | ASP.NET Core Web API | Accepts vote submissions quickly, publishes work to RabbitMQ through MassTransit, exposes status lookup and SignalR result updates. |
| `AsynchronousVoting.Worker` | Worker service | Consumes async vote commands, persists votes, emits vote-recorded events, updates projections, records latency metrics. |
| `HybridVoting.Api` | ASP.NET Core Web API | Writes the core vote synchronously, then publishes secondary projection/audit work to RabbitMQ. |
| `Hybrid.Worker` | Worker service | Consumes hybrid vote-recorded events and applies projection/audit side effects. |
| `Voting.LoadTests` | Console app | NBomber scenarios for steady, staircase, burst, scaled, and projection-delay experiments. |

## Architecture

The backend follows a clean architecture style:

```text
API / Worker hosts
    v
Voting.Application
    v
Voting.Domain
    ^
Voting.Infrastructure
```

The API and worker hosts compose the application and infrastructure layers through dependency injection. Domain objects are kept in `Voting.Domain`; business workflow services live in `Voting.Application`; SQL Server and EF Core details live in `Voting.Infrastructure`.

### Shared Domain Model

Core entities:

- `Poll` and `PollOption` define available polls and possible answers.
- `VoteRecord` stores counted votes.
- `VoteSubmission` tracks accepted asynchronous submissions and their lifecycle.
- `VoterEligibility` stores eligibility state and check counters.
- `VoteAuditLog` stores audit entries for accepted votes.
- `PollResultsProjection` and `PollOptionResultsProjection` store precomputed read-model data.

Important data rules:

- each poll can have many options and many votes,
- `Votes` has a unique filtered index on `(PollId, UserId)` when `UserId` is not null, preventing duplicate votes by the same user in one poll,
- vote status is stored as a string enum,
- projections are keyed by poll and option IDs,
- seed data creates three active benchmark polls with Polish question/answer text.

### Application Services

Main services:

- `VotingService` - synchronous vote orchestration.
- `VoteWriteService` - validates and persists vote records.
- `VoteValidationService` - checks request shape, poll existence, active poll state, option membership, eligibility, and duplicate voting.
- `EligibilityService` - loads or creates eligibility state and rejects ineligible users.
- `VoteProjectionAndAuditService` - applies projection updates and writes audit logs.
- `PollService` - reads polls and results.
- `ProjectionPollResultsReader` - reads result projections.
- `AuthoritativeVoteResultsReader` - can read results directly from recorded votes.

The default registration currently maps `IPollResultsReader` to `ProjectionPollResultsReader`, so result endpoints use the projection read model.

## Implemented Runtime Architectures

### 1. Synchronous Architecture

Project: `src/SynchronousVoting.Api`

Default URL: `http://localhost:5001`

Flow:

```text
Client
  -> POST /api/vote
  -> validate vote
  -> check eligibility and duplicates
  -> write VoteRecord to SQL Server
  -> update projection and audit log
  -> return VoteResponse
```

Characteristics:

- no RabbitMQ worker is required for the main vote path,
- vote response confirms the vote has been counted,
- read model is updated before the request completes,
- useful baseline for latency and throughput comparisons,
- results are usually consumed by the frontend through polling.

### 2. Asynchronous Event-Driven Architecture

Projects:

- `src/AsynchronousVoting.Api`
- `src/AsynchronousVoting.Worker`

Default API URL: `http://localhost:5002`

Default worker metrics URL: `http://localhost:9184/metrics`

Flow:

```text
Client
  -> POST /api/vote
  -> API creates VoteSubmission
  -> API publishes CastVoteCommand through MassTransit/RabbitMQ
  -> API returns accepted submission
  -> Worker consumes cast-vote queue
  -> Worker validates and writes VoteRecord
  -> Worker publishes VoteRecordedEvent
  -> Worker/projector updates projection and audit log
  -> API receives PollResultsUpdatedEvent
  -> API pushes SignalR update to clients
```

Characteristics:

- lower HTTP response latency under load because the request is accepted before full processing,
- eventual consistency for vote results,
- status lookup is available through `GET /api/vote/status/{submissionId}`,
- queue delay and worker execution latency are measured separately,
- MassTransit EF Core outbox is used to make database writes and bus publication more reliable.

Queues/exchanges are centralized in `Voting.Application/Messaging/VoteQueueNames.cs` and related MassTransit configuration.

### 3. Hybrid Architecture

Projects:

- `src/HybridVoting.Api`
- `src/Hybrid.Worker`

Default API URL: `http://localhost:5003`

Default worker metrics URL: `http://localhost:9284/metrics`

Flow:

```text
Client
  -> POST /api/vote
  -> API validates and writes VoteRecord synchronously
  -> API returns after core write
  -> API/Outbox publishes VoteRecordedEvent
  -> Hybrid worker updates projection and audit log
  -> API receives PollResultsUpdatedEvent
  -> API pushes SignalR update to clients
```

Characteristics:

- core vote persistence is synchronous,
- projection/audit side effects are asynchronous,
- provides a middle ground between immediate durability and eventual read-model updates,
- useful for comparing user-perceived latency against consistency guarantees.

## API Surface

Shared endpoints across the APIs:

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/api/polls` | Return active polls with options. |
| `GET` | `/api/polls/{id}` | Return one poll with options. |
| `POST` | `/api/vote` | Submit a vote. Response shape depends on architecture. |
| `GET` | `/api/results` | Return current poll results. |
| `POST` | `/api/metrics/ux/vote-latency` | Accept frontend UX latency samples. |
| `GET` | `/health` or `/health/live`, `/health/ready` | Health checks. |
| `GET` | `/metrics` | Prometheus metrics endpoint. |

Asynchronous-only endpoint:

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/api/vote/status/{submissionId}` | Return async submission status and latency timestamps. |

SignalR hubs:

- synchronous API maps `/hubs/results` and `/hubs/votes`,
- async and hybrid APIs map `/hubs/results`,
- result notifications use `PollResultsUpdated`.

## Configuration

Each host has an `appsettings.json` and, where present, an `appsettings.Development.example.json`.

Default local ports:

| Component | Port |
| --- | --- |
| Synchronous API | `5001` |
| Asynchronous API | `5002` |
| Hybrid API | `5003` |
| Asynchronous worker metrics | `9184` |
| Hybrid worker metrics | `9284` |
| SQL Server | `1433` |
| RabbitMQ | `5672` |
| Prometheus | `9091` |
| Grafana | `3000` |

Important settings:

- `ConnectionStrings:DefaultConnection` - SQL Server database connection.
- `RabbitMq:Host`, `RabbitMq:Username`, `RabbitMq:Password` - RabbitMQ connection for async and hybrid hosts.
- `OtlpExporter:Endpoint` - OpenTelemetry collector endpoint when used.
- `Hosting:MetricsPort` - worker Prometheus listener port.
- `Worker:ConcurrentMessageLimit` and `Worker:PrefetchCount` - worker throughput controls.
- `Chaos__ProjectionDelayMs` or `CHAOS_PROJECTION_DELAY_MS` - optional artificial projection delay used by experiments.

For real deployment, move credentials out of committed `appsettings.json` files into user secrets, environment variables, or secret management.

## Local Setup

Prerequisites:

- .NET 9 SDK,
- Docker Desktop or compatible Docker runtime,
- SQL Server and RabbitMQ available locally,
- optional: Prometheus and Grafana for metrics,
- optional: Node.js and the frontend repository for UX tests.

Start SQL Server from the sibling monitoring directory:

```bash
cd ../monitoring
docker compose -f sql_server_compose.yml up -d
```

Start Prometheus and Grafana:

```bash
cd ../monitoring
docker compose up -d
```

RabbitMQ is required for asynchronous and hybrid runs. If you do not already have it running locally, start a RabbitMQ container that exposes ports `5672` and, optionally, `15672` for the management UI.

Restore and build:

```bash
dotnet restore Voting.sln
dotnet build Voting.sln
```

Run one architecture:

```bash
dotnet run --project src/SynchronousVoting.Api/SynchronousVoting.Api.csproj
```

```bash
dotnet run --project src/AsynchronousVoting.Api/AsynchronousVoting.Api.csproj
dotnet run --project src/AsynchronousVoting.Worker/AsynchronousVoting.Worker.csproj
```

```bash
dotnet run --project src/HybridVoting.Api/HybridVoting.Api.csproj
dotnet run --project src/Hybrid.Worker/Hybrid.Worker.csproj
```

Swagger is available on the API host when enabled by the environment, for example `http://localhost:5001/swagger`.

## Database

The database is managed through EF Core in `Voting.Infrastructure`.

The application applies migrations during startup through `app.ApplyMigrations()`. The model includes:

- poll and option tables,
- votes and vote submissions,
- eligibility records,
- audit logs,
- projection tables,
- MassTransit transactional outbox/inbox tables.

Useful commands:

```bash
dotnet ef migrations add MigrationName \
  --project src/Voting.Infrastructure \
  --startup-project src/SynchronousVoting.Api
```

```bash
dotnet ef database update \
  --project src/Voting.Infrastructure \
  --startup-project src/SynchronousVoting.Api
```

Use the appropriate startup project when targeting async or hybrid databases.

## Observability

The APIs and workers expose OpenTelemetry metrics. Prometheus scrapes:

- sync API instances on `5001`, `5101`, `5201`,
- async API instances on `5002`, `5102`, `5202`,
- hybrid API instances on `5003`, `5103`, `5203`,
- async workers on `9184`, `9185`, `9186`,
- hybrid workers on `9284`, `9285`, `9286`.

Measured areas include:

- HTTP request duration,
- vote HTTP response latency,
- vote processing duration,
- queue delay,
- worker execution duration,
- UX vote latency reported by the frontend,
- runtime and process metrics.

Grafana is configured through the monitoring stack and can render panels for automated experiment output.

## Load And Experiment Runner

`src/Voting.LoadTests` contains NBomber-based load tests. It reads the target API from environment variables:

- `VOTING_API_BASE_URL` for one API instance,
- `VOTING_API_BASE_URLS` for scaled runs,
- `ARCHITECTURE` for report naming,
- `LOAD_PROFILE` for scenario selection,
- `STEADY_RPS`, `STEADY_MINUTES`, `STAIR_RATES`, `STAIR_STEP_MINUTES` for profiles.

Example:

```bash
VOTING_API_BASE_URL=http://localhost:5001 \
ARCHITECTURE=sync \
LOAD_PROFILE=steady \
STEADY_RPS=30 \
STEADY_MINUTES=6 \
dotnet run --project src/Voting.LoadTests/Voting.LoadTests.csproj
```

The top-level `run_all.sh` script orchestrates repeated experiments across architectures and scenarios. It can reset databases, purge queues, start processes, run NBomber, run frontend UX probes, collect Prometheus/Grafana data, and write output under `test_results/automated/<run-id>/`.

Common environment switches:

- `ARCHITECTURES=sync,async,hybrid`
- `SCENARIOS_SINGLE=steady_5rps,staircase_5_10_50_100`
- `SCENARIOS_SCALED=steady_5rps,staircase_5_10_50_100,burst_5_100_5`
- `REPEATS=1`
- `ONLY_SINGLE=1`
- `ONLY_SCALED=0`
- `UX_PROBE_ENABLED=1`
- `PROM_URL=http://localhost:9091/api/v1/query`

Run:

```bash
./run_all.sh
```

Generated files include NBomber CSV/HTML reports, backend logs, resource time series, UX probe JSONL files, and Grafana panel screenshots.

## Test Scenario Design

The detailed automated experiment design is implemented in `automate_tests.py`. The script compares the same load profile across the selected backend architectures, then writes a semicolon-delimited summary row for every `(architecture, scenario)` pair.

Default architecture set:

| Architecture key | Default API URL | Runtime under test |
| --- | --- | --- |
| `sync` | `http://localhost:5001` | `SynchronousVoting.Api` |
| `async` | `http://localhost:5002` | `AsynchronousVoting.Api` plus `AsynchronousVoting.Worker` |
| `hybrid` | `http://localhost:5003` | `HybridVoting.Api` plus `Hybrid.Worker` |

The API targets can be overridden with:

- `BASE_URL_SYNC`, `BASE_URL_ASYNC`, `BASE_URL_HYBRID` for one instance,
- `BASE_URLS_SYNC`, `BASE_URLS_ASYNC`, `BASE_URLS_HYBRID` for scaled tests with several comma-separated instances.

### Scenario Matrix

`automate_tests.py` defines these scenario profiles:

| Scenario | Load profile | Target rate | Duration | Environment passed to NBomber |
| --- | --- | --- | --- | --- |
| `steady_30rps` | steady | `30` RPS | 6 minutes | `STEADY_RPS=30`, `STEADY_MINUTES=6` |
| `staircase_30_50_100_200` | staircase | average of `30,50,100,200` RPS | 8 minutes | `STAIR_RATES=30,50,100,200`, `STAIR_STEP_MINUTES=2` |
| `burst_10_150_10` | burst | average of `10,150,10` RPS | 3 minutes | `BURST_RATES=10,150,10`, `BURST_STEP_MINUTES=1` |
| `projection_delay_500ms` | steady with projection delay | `20` RPS | 5 minutes | `STEADY_RPS=20`, `STEADY_MINUTES=5`, `Chaos__ProjectionDelayMs=500` |

Design intent:

- `steady_30rps` measures stable behavior under a constant moderate load.
- `staircase_30_50_100_200` measures degradation as load increases in controlled two-minute steps.
- `burst_10_150_10` measures elasticity and recovery when traffic jumps sharply and then falls back.
- `projection_delay_500ms` injects artificial projection latency to stress eventual consistency, SignalR freshness, queue behavior, and perceived frontend latency.

For steady scenarios the target RPS is the configured `STEADY_RPS`. For staircase and burst scenarios the script calculates the comparison target as the arithmetic mean of the configured rates and reports `RPS_Drift_Percent` against that target.

### Per-Scenario Execution Flow

For each selected architecture and scenario, `automate_tests.py` performs this sequence:

```text
resolve Prometheus URL
for each architecture
  for each scenario
    prepare output paths
    set ARCHITECTURE and LOAD_PROFILE
    apply scenario-specific environment variables
    choose single URL or comma-separated scaled URLs
    optionally purge RabbitMQ queues
    optionally start Angular frontend for the architecture
    optionally start Playwright UX probe
    start resource monitor with 1 second sampling
    run NBomber through Voting.LoadTests
    stop resource monitor, UX probe, and frontend
    wait for metrics settle period
    parse latest NBomber CSV report
    query Prometheus for backend, HTTP, queue, worker, and UX metrics
    aggregate Playwright JSONL samples
    optionally export Grafana panels
    append one result row to the output CSV
```

The runner invokes NBomber with:

```bash
dotnet run --project src/Voting.LoadTests/Voting.LoadTests.csproj -- <base-url> <architecture> <load-profile>
```

When scaled URLs are provided, they are passed to NBomber through `VOTING_API_BASE_URLS`; otherwise the single selected URL is passed through `VOTING_API_BASE_URL`.

### Queue And Resource Sampling

During every load run, the script starts a `ResourceMonitor` thread with a one-second interval. It writes a per-run time series CSV under:

```text
<artifacts-dir>/resource_series/<output-file-stem>/<architecture>/<scenario>.csv
```

Captured resource columns:

- `ts_epoch`,
- `elapsed_s`,
- `architecture`,
- `queue_depth_primary`,
- `queue_depth_secondary`,
- `queue_depth_total`,
- `worker_cpu_pct`,
- `worker_ram_mb`,
- `db_cpu_pct`,
- `db_ram_mb`.

Queue names tracked by architecture:

| Architecture | Primary queue | Secondary queue |
| --- | --- | --- |
| `async` | `cast-vote-queue` | `async-poll-results-updated-events` |
| `hybrid` | `hybrid-vote-recorded-events` | `hybrid-poll-results-updated-events` |
| `sync` | none | none |

Worker CPU/RAM is sampled by process name patterns:

- async worker: `AsynchronousVoting.Worker`, `AsynchronousVoting.Worker.dll`,
- hybrid worker: `Hybrid.Worker`, `Hybrid.Worker.dll`.

Database CPU/RAM is sampled from the Docker container named `voting-db-mssql` by default. This can be changed with `DB_CONTAINER_NAME`; process fallback matching uses `DB_PROCESS_PATTERNS`, defaulting to `sqlservr`.

### UX Probe Design

Unless `--disable-ux-probe` is used, the script starts the Angular frontend and runs the Playwright probe from the frontend repository:

```text
Frontend app: ../Frontend/voting-app-spa
Probe file:   playwright/vote_ux_probe.spec.ts
Config file:  playwright.config.ts
UI URL:       http://127.0.0.1:4200 by default
```

The frontend is started with the Angular configuration matching the backend architecture:

| Architecture | Angular configuration | Runtime hub path |
| --- | --- | --- |
| `sync` | `synchronous` | `/hubs/votes` |
| `async` | `asynchronous` | `/hubs/results` |
| `hybrid` | `hybrid` | `/hubs/results` |

Before starting Angular, the script rewrites `src/assets/runtime-config.js` so the running frontend points at the backend URL currently under test.

The Playwright probe runs for:

```text
scenario duration in seconds + UX_DURATION_BUFFER_SECONDS
```

The default buffer is 30 seconds. Probe output is stored as JSONL:

```text
<artifacts-dir>/ux_probe/<architecture>/<scenario>.jsonl
```

The runner aggregates UX samples into:

- `E2E_Mean`, `E2E_p50`, `E2E_p95`,
- `TVF_Mean`, `TVF_p50`, `TVF_p95`,
- `DCL_Mean`, `DCL_p50`, `DCL_p95`,
- `UX_Samples`.

### Metrics Collected

The result CSV produced by `automate_tests.py` contains these major metric groups:

| Metric group | Columns |
| --- | --- |
| Run metadata | `Timestamp`, `Architecture`, `Scenario`, `Target_RPS`, `RPS_Drift_Percent` |
| NBomber output | `RPS_Actual`, `NBomber_Mean`, `NBomber_p50`, `NBomber_p95`, `Fail_Rate_Percent` |
| Backend processing | `Backend_Mean`, `Grafana_p50_Processing`, `Grafana_p95_Processing`, `Grafana_p99_Processing` |
| HTTP POST latency | `Grafana_p50_Latency_POST`, `Grafana_p95_Latency_POST`, `Grafana_p99_Latency_POST` |
| Queue/worker latency | `QueueDelay_p50`, `QueueDelay_p95`, `WorkerExecution_p50`, `WorkerExecution_p95` |
| Resource pressure | `Rabbit_QueueDepth_Max`, `Worker_CPU_Max`, `Worker_RAM_Max`, `DB_CPU_Max`, `DB_RAM_Max` |
| HTTP resilience | `HTTP_429_Percent`, `Timeout_Percent` |
| Frontend UX metrics | `UX_Mean`, `UX_p99`, `UX_p95`, `UX_p50`, `UX_Samples`, `E2E_*`, `TVF_*`, `DCL_*` |
| Quality flags and artifacts | `Processing_p95_Capped`, `UX_p95_Capped`, `Resource_Series_File`, `UX_Probe_File`, `NBomber_Report` |

Prometheus queries use the scenario duration as their lookback window. The script treats backend mean, backend p95, and POST p95 as required metrics; missing required values fail the collection step unless `--continue-on-error` is supplied.

### Grafana Export

If Grafana settings are provided, the script exports selected panel screenshots for the exact test interval:

- `GRAFANA_URL`,
- `GRAFANA_DASHBOARD_UID`,
- `GRAFANA_DASHBOARD_SLUG`, default `load-tests`,
- `GRAFANA_PANEL_IDS`,
- `GRAFANA_ORG_ID`, default `1`,
- `GRAFANA_WIDTH`, default `1600`,
- `GRAFANA_HEIGHT`, default `900`,
- `GRAFANA_API_TOKEN`, optional,
- `GRAFANA_ARCH_VAR`, default `architecture`.

Exported panels are written to:

```text
<artifacts-dir>/panels/<architecture>/<scenario>/panel_<id>.png
```

### Direct Runner Usage

Run the scenario matrix directly:

```bash
python3 automate_tests.py \
  --architectures sync,async,hybrid \
  --scenarios steady_30rps,staircase_30_50_100_200,burst_10_150_10,projection_delay_500ms \
  --output test_results/automated/results_pairs.csv \
  --artifacts-dir test_results/automated/manual_run \
  --prom-url http://localhost:9091/api/v1/query \
  --purge-queues \
  --continue-on-error
```

Useful flags:

- `--skip-run` parses the latest NBomber report and collects Prometheus metrics without running a new load test.
- `--disable-ux-probe` runs backend-only experiments.
- `--settle-seconds` controls the wait before metric collection, default `60`.
- `--prom-debug` prints the Prometheus query selected for each metric.
- `--fail-on-grafana-export` makes panel export failures fail the run.
- `--frontend-dir` points to the Angular app, default `../Frontend/voting-app-spa`.
- `--ui-base-url` sets the local UI URL used by the probe.

Note: the script argument default currently references older scenario names. Pass the scenario list above explicitly, or run through `run_all.sh`, which supplies its own scenario configuration.

## Development Notes

- Keep domain rules in `Voting.Domain` and `Voting.Application`; keep EF Core and broker details in `Voting.Infrastructure` or host projects.
- Add shared API behavior to `Voting.Api.Common` when all API hosts need it.
- When adding a new event or queue, update both producer and consumer MassTransit configuration.
- When adding new read-model fields, update domain projection entities, EF configuration, projection mapping, DTOs, and frontend models.
- The existing worktree may contain generated experiment artifacts; avoid committing `bin`, `obj`, `nbomber-reports`, and `test_results` unless they are intentionally part of a research snapshot.
