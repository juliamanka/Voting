using AsynchronousVoting.Worker.Messaging.Consumers;
using MassTransit;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Voting.Application;
using Voting.Application.DTOs;
using Voting.Application.Messaging;
using Voting.Application.Options;
using Voting.Infrastructure;
using Voting.Infrastructure.Database;

var configName = args
    .FirstOrDefault(a => a.StartsWith("--config=", StringComparison.OrdinalIgnoreCase))
    ?.Split('=', 2)[1];

var builder = Host.CreateApplicationBuilder(args);

if (!string.IsNullOrWhiteSpace(configName))
{
    builder.Configuration.AddJsonFile(
        $"appsettings.{configName}.json",
        optional: false,
        reloadOnChange: true);
}

var metricsPort = builder.Configuration.GetValue<int?>("Hosting:MetricsPort") ?? 9184;
var workerConcurrency = builder.Configuration.GetValue<int?>("Worker:ConcurrentMessageLimit") ?? 4;
var workerPrefetch = builder.Configuration.GetValue<ushort?>("Worker:PrefetchCount") ?? 8;
var projectionConcurrency = builder.Configuration.GetValue<int?>("Worker:ProjectionConcurrentMessageLimit") ?? workerConcurrency;
var projectionPrefetch = builder.Configuration.GetValue<ushort?>("Worker:ProjectionPrefetchCount") ?? workerPrefetch;

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.Configure<ProjectionOptions>(options =>
{
    options.DelayMs = builder.Configuration.GetValue<int?>("Chaos:ProjectionDelayMs")
                      ?? ReadPositiveIntEnvironmentVariable("CHAOS_PROJECTION_DELAY_MS");
});

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<CastVoteCommandConsumer>();
    x.AddConsumer<VoteRecordedEventConsumer>();
    x.AddEntityFrameworkOutbox<VotingDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });

        cfg.Message<PollResultsUpdatedEvent>(m => m.SetEntityName("async-poll-results-updated-exchange"));
        cfg.Message<VoteRecordedEvent>(m => m.SetEntityName("async-vote-recorded-exchange"));

        cfg.ReceiveEndpoint(VoteQueueNames.AsyncCastVoteQueue, e =>
        {
            e.UseEntityFrameworkOutbox<VotingDbContext>(context);
            e.UseMessageRetry(ConfigurePostgresTransientRetry);

            e.ConcurrentMessageLimit = workerConcurrency;
            e.PrefetchCount = workerPrefetch;

            e.ConfigureConsumer<CastVoteCommandConsumer>(context);
        });

        cfg.ReceiveEndpoint(VoteQueueNames.AsyncVoteRecordedEventsQueue, e =>
        {
            e.UseEntityFrameworkOutbox<VotingDbContext>(context);
            e.UseMessageRetry(ConfigurePostgresTransientRetry);

            e.ConcurrentMessageLimit = projectionConcurrency;
            e.PrefetchCount = projectionPrefetch;

            e.ConfigureConsumer<VoteRecordedEventConsumer>(context);
        });
    });
});

const string serviceName = "AsynchronousVoting.Worker";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithMetrics(metrics => metrics
        .AddMeter("AsynchronousVoting.Worker.Metrics")
        .AddView("vote_durable_write_duration_seconds", new ExplicitBucketHistogramConfiguration
        {
            Boundaries = new[]
            {
                0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5,
                1, 2, 5, 10, 20, 30, 45, 60, 90, 120, 180, 240, 300
            }
        })
        .AddView("vote_projection_completion_duration_seconds", new ExplicitBucketHistogramConfiguration
        {
            Boundaries = new[]
            {
                0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5,
                1, 2, 5, 10, 20, 30, 45, 60, 90, 120, 180, 240, 300
            }
        })
        .AddView("vote_recorded_event_queue_delay_seconds", new ExplicitBucketHistogramConfiguration
        {
            Boundaries = new[]
            {
                0.001, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5,
                1, 2, 5, 10, 20, 30, 45, 60, 90, 120, 180, 240, 300
            }
        })
        .AddView("vote_submission_queue_delay_seconds", new ExplicitBucketHistogramConfiguration
        {
            Boundaries = new[]
            {
                0.001, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5,
                1, 2, 5, 10, 20, 30, 45, 60, 90, 120, 180, 240, 300
            }
        })
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddPrometheusHttpListener(options =>
        {
            options.UriPrefixes = new[]
            {
                $"http://+:{metricsPort}/"
            };
        }));

var host = builder.Build();
host.Run();

static void ConfigurePostgresTransientRetry(IRetryConfigurator retry)
{
    retry.Handle<NpgsqlException>(ex => ex.IsTransient);
    retry.Handle<TimeoutException>();
    retry.Intervals(
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromMilliseconds(500));
}


static int ReadPositiveIntEnvironmentVariable(string name)
{
    var raw = Environment.GetEnvironmentVariable(name);
    return int.TryParse(raw, out var value) && value > 0 ? value : 0;
}
