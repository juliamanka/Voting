# Voting Backend

Backend prototype for a master's thesis comparing three communication styles in a real-time voting system:

- synchronous API-only processing,
- asynchronous event-driven processing,
- hybrid processing with a synchronous core write and asynchronous projection/audit work.

The solution is written in .NET 9 and uses ASP.NET Core, Entity Framework Core, PostgreSQL, MassTransit, RabbitMQ, SignalR, OpenTelemetry, Prometheus, Serilog, FluentValidation, AutoMapper, and NBomber.

## Repository Layout

```text
Voting/
|-- Voting.sln
|-- Voting.Api.Common/
|   |-- ApiHostExtensions.cs          Shared API host setup
|   |-- Contracts/Monitoring/         Shared monitoring DTOs
|   |-- Controllers/                  Shared poll/result controller bases
|   |-- Middleware/                   Global exception handling
|   |-- RequestTiming/                Request timing context
|   `-- ExceptionHandlingExtensions.cs
|-- src/
|   |-- Voting.Domain/                Domain entities, enums, repository contracts
|   |-- Voting.Application/           DTOs, validators, service interfaces and services
|   |-- Voting.Infrastructure/        EF Core DbContext, migrations, repositories
|   |-- SynchronousVoting.Api/        Synchronous HTTP API
|   |-- AsynchronousVoting.Api/       Async HTTP API and SignalR notification consumer
|   |-- AsynchronousVoting.Worker/    Async vote and projection consumers
|   |-- HybridVoting.Api/             Hybrid HTTP API and SignalR notification consumer
|   |-- Hybrid.Worker/                Hybrid projection/audit consumer
|   `-- Voting.LoadTests/             NBomber load test runner
`-- nginx-lb.conf                     Load-balancer config for scaled local tests
```

Generated outputs such as `bin/`, `obj/`, `.idea/`, `.DS_Store`, `test_results/`, and local appsettings files are ignored by the root `.gitignore`.

## Projects

| Project | Responsibility |
| --- | --- |
| `Voting.Domain` | Core entities for polls, options, vote records, vote submissions, audit logs, and read-model projections. |
| `Voting.Application` | Application services, DTOs, validation, mapping, and queue naming. |
| `Voting.Infrastructure` | PostgreSQL persistence, EF Core migrations, repository implementations, and MassTransit outbox entities. |
| `Voting.Api.Common` | Shared API setup, error handling, timing, monitoring contracts, and base controllers. |
| `SynchronousVoting.Api` | Baseline API-only variant. Vote validation, durable write, projection update, audit write, and response happen in one request. |
| `AsynchronousVoting.Api` | Event-driven API variant. The API accepts a vote submission and queues work for background processing. |
| `AsynchronousVoting.Worker` | Consumes async vote commands, writes votes, updates projections, emits notifications, and records worker metrics. |
| `HybridVoting.Api` | Hybrid variant. The API writes the vote synchronously and publishes projection/audit work asynchronously. |
| `Hybrid.Worker` | Consumes hybrid vote-recorded events and applies projection/audit side effects. |
| `Voting.LoadTests` | NBomber scenarios for throughput, latency, and scaling comparisons. |

## Architecture

```text
API / Worker hosts
    v
Voting.Application
    v
Voting.Domain
    ^
Voting.Infrastructure
```

API and worker hosts compose the application and infrastructure layers through dependency injection. Domain objects stay in `Voting.Domain`; workflow logic is in `Voting.Application`; persistence and EF Core details are in `Voting.Infrastructure`.

## Runtime Variants

### Synchronous

Project: `src/SynchronousVoting.Api`

Default URL: `http://localhost:5001`

```text
POST /api/vote
  -> validate vote
  -> check duplicate vote for the same user and poll
  -> write VoteRecord to PostgreSQL
  -> update projection and audit log
  -> send SignalR result update
  -> return VoteResponse
```

This variant is the API-only baseline. It gives immediate consistency for the read model, but the user waits for the full workflow.

### Asynchronous

Projects:

- `src/AsynchronousVoting.Api`
- `src/AsynchronousVoting.Worker`

Default API URL: `http://localhost:5002`

Default worker metrics URL: `http://localhost:9184/metrics`

```text
POST /api/vote
  -> validate request
  -> create VoteSubmission
  -> publish CastVoteCommand to RabbitMQ
  -> return VoteAcceptedResponse
worker
  -> consume CastVoteCommand
  -> write VoteRecord
  -> publish VoteRecordedEvent
  -> update projection and audit log
  -> publish PollResultsUpdatedEvent
API
  -> consume PollResultsUpdatedEvent
  -> send SignalR result update
```

This variant prioritizes fast HTTP acceptance and eventual consistency. Submission status is available through `GET /api/vote/status/{submissionId}`.

### Hybrid

Projects:

- `src/HybridVoting.Api`
- `src/Hybrid.Worker`

Default API URL: `http://localhost:5003`

Default worker metrics URL: `http://localhost:9284/metrics`

```text
POST /api/vote
  -> validate vote
  -> write VoteRecord to PostgreSQL
  -> publish VoteRecordedEvent through the outbox
  -> return VoteResponse
worker
  -> update projection and audit log
  -> publish PollResultsUpdatedEvent
API
  -> consume PollResultsUpdatedEvent
  -> send SignalR result update
```

This variant keeps the core vote durable before responding while moving projection/audit side effects out of the request path.

## API Surface

Shared endpoints:

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/api/polls` | Return active polls with options. |
| `GET` | `/api/polls/{id}` | Return one poll with options. |
| `POST` | `/api/vote` | Submit a vote. Response shape depends on architecture. |
| `GET` | `/api/results` | Return current projected poll results. |
| `POST` | `/api/metrics/ux/vote-latency` | Accept frontend UX latency samples. |
| `GET` | `/metrics` | Prometheus scraping endpoint. |

Health endpoints:

- sync API: `/health/live` and `/health/ready`,
- async and hybrid APIs: `/health`.

SignalR hubs:

- sync API: `/hubs/results` and `/hubs/votes`,
- async and hybrid APIs: `/hubs/results`.

## Configuration

Local configuration can be provided through ignored `appsettings.json` files or environment variables.

Important settings:

- `ConnectionStrings:DefaultConnection` - PostgreSQL connection string.
- `RabbitMq:Host`, `RabbitMq:Username`, `RabbitMq:Password` - RabbitMQ connection for async and hybrid variants.
- `Hosting:MetricsPort` - worker metrics listener port.
- `Worker:ConcurrentMessageLimit`, `Worker:PrefetchCount` - worker throughput controls.
- `Chaos__ProjectionDelayMs` or `CHAOS_PROJECTION_DELAY_MS` - optional artificial projection delay for experiments.

Default local ports:

| Component | Port |
| --- | --- |
| Synchronous API | `5001` |
| Asynchronous API | `5002` |
| Hybrid API | `5003` |
| Asynchronous worker metrics | `9184` |
| Hybrid worker metrics | `9284` |
| PostgreSQL | `5432` |
| RabbitMQ | `5672` |

## Local Setup

Prerequisites:

- .NET 9 SDK,
- PostgreSQL,
- RabbitMQ for async and hybrid variants,
- optional Prometheus/Grafana for metrics collection.

Restore and build:

```bash
dotnet restore Voting.sln
dotnet build Voting.sln
```

Run the synchronous API:

```bash
dotnet run --project src/SynchronousVoting.Api/SynchronousVoting.Api.csproj
```

Run the asynchronous variant:

```bash
dotnet run --project src/AsynchronousVoting.Api/AsynchronousVoting.Api.csproj
dotnet run --project src/AsynchronousVoting.Worker/AsynchronousVoting.Worker.csproj
```

Run the hybrid variant:

```bash
dotnet run --project src/HybridVoting.Api/HybridVoting.Api.csproj
dotnet run --project src/Hybrid.Worker/Hybrid.Worker.csproj
```

Swagger is available when enabled by the running environment, for example `http://localhost:5001/swagger`.

## Database

The application applies EF Core migrations during startup through `app.ApplyMigrations()`. The model includes:

- polls and poll options,
- votes and async vote submissions,
- vote audit logs,
- projection tables,
- MassTransit transactional inbox/outbox tables.

EF Core design-time commands should be run from the repository root. `Voting.Infrastructure` contains the `DbContext` and migrations, while the selected API project supplies configuration such as `ConnectionStrings:DefaultConnection`.

Add a migration:

```bash
dotnet ef migrations add MigrationName \
  --project src/Voting.Infrastructure \
  --startup-project src/SynchronousVoting.Api
```

Apply migrations:

```bash
dotnet ef database update \
  --project src/Voting.Infrastructure \
  --startup-project src/SynchronousVoting.Api
```

Use the API startup project for the database you want to target:

```bash
# synchronous database/configuration
--startup-project src/SynchronousVoting.Api

# asynchronous database/configuration
--startup-project src/AsynchronousVoting.Api

# hybrid database/configuration
--startup-project src/HybridVoting.Api
```

If you run the command from inside `src/Voting.Infrastructure`, adjust the relative paths:

```bash
dotnet ef database update \
  --project . \
  --startup-project ../AsynchronousVoting.Api
```

## Observability

The APIs and workers expose OpenTelemetry metrics for:

- HTTP request duration,
- vote HTTP response latency,
- durable vote write duration,
- projection completion duration,
- queue delay,
- SignalR notification duration,
- UX vote latency reported by the frontend,
- runtime and process metrics.

The metrics are intentionally aligned across architectures so thesis comparisons can use the same metric names where the concepts overlap.

## Load Testing

`src/Voting.LoadTests` contains NBomber-based load tests. It reads:

- `VOTING_API_BASE_URL` for one API instance,
- `VOTING_API_BASE_URLS` for scaled runs,
- `ARCHITECTURE` for report naming,
- `LOAD_PROFILE` for scenario selection,
- `STEADY_RPS`, `STEADY_MINUTES`, `STAIR_RATES`, `STAIR_STEP_MINUTES`, `BURST_RATES`, `BURST_STEP_MINUTES` for profile parameters.

Example:

```bash
VOTING_API_BASE_URL=http://localhost:5001 \
ARCHITECTURE=sync \
LOAD_PROFILE=steady \
STEADY_RPS=30 \
STEADY_MINUTES=6 \
dotnet run --project src/Voting.LoadTests/Voting.LoadTests.csproj
```

Use `nginx-lb.conf` with multiple API instances when running scaled local experiments.
