using Hybrid.Worker.Messaging.Consumers;
using MassTransit;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Voting.Application;
using Voting.Application.DTOs;
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

var metricsPort = builder.Configuration.GetValue<int?>("Hosting:MetricsPort") ?? 9284;
var workerConcurrency = builder.Configuration.GetValue<int?>("Worker:ConcurrentMessageLimit") ?? 8;
var workerPrefetch = builder.Configuration.GetValue<ushort?>("Worker:PrefetchCount") ?? 16;
var enableProjectionProjector = builder.Configuration.GetValue<bool?>("Worker:EnableProjectionProjector") ?? true;

// 1. Application + Infrastructure
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();

// 2. MassTransit + RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<VoteRecordedEventConsumer>();
    x.AddEntityFrameworkOutbox<VotingDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
    });
    
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });

        if (enableProjectionProjector)
        {
            cfg.Message<PollResultsUpdatedEvent>(m => m.SetEntityName("hybrid-poll-results-updated-exchange"));
            cfg.ReceiveEndpoint("hybrid-vote-recorded-events", e =>
            {
                e.UseEntityFrameworkOutbox<VotingDbContext>(context);
                e.UseMessageRetry(ConfigureSqlTransientRetry);
                e.ConfigureConsumer<VoteRecordedEventConsumer>(context);
                e.ConcurrentMessageLimit = workerConcurrency;
                e.PrefetchCount = workerPrefetch;
            });
        }
    });
});

const string serviceName = "Hybrid.Worker";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithMetrics(metrics => metrics
        .AddMeter("HybridVoting.Worker.Metrics")
        .AddView("vote_processing_duration_seconds", new ExplicitBucketHistogramConfiguration
        {
            Boundaries = new[]
            {
                0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5,
                1, 2, 5, 10, 20, 30, 45, 60, 90, 120, 180, 240, 300
            }
        })
        .AddView("vote_queue_delay_seconds", new ExplicitBucketHistogramConfiguration
        {
            Boundaries = new[]
            {
                0.001, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5,
                1, 2, 5, 10, 20, 30, 45, 60, 90, 120, 180, 240, 300
            }
        })
        .AddView("vote_worker_execution_duration_seconds", new ExplicitBucketHistogramConfiguration
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
            options.UriPrefixes = new[] { 
                $"http://*:{metricsPort}/"
            };
        }));

var host = builder.Build();
host.Run();

static void ConfigureSqlTransientRetry(IRetryConfigurator retry)
{
    retry.Handle<Microsoft.Data.SqlClient.SqlException>(IsTransientSqlException);
    retry.Handle<InvalidOperationException>(ex =>
        ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlException &&
        IsTransientSqlException(sqlException));
    retry.Handle<TimeoutException>();
    retry.Intervals(
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromMilliseconds(500));
}

static bool IsTransientSqlException(Microsoft.Data.SqlClient.SqlException exception)
{
    return exception.Number is 1205 or -2 or 4060 or 40197 or 40501 or 40613 or 49918 or 49919 or 49920;
}
