using AsynchronousVoting.Worker.Messaging.Consumers;
using MassTransit;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Voting.Application;
using Voting.Infrastructure;

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

// 1. Application + Infrastructure
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();

// 2. MassTransit + RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<CastVoteConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });

        cfg.ReceiveEndpoint("cast-vote-queue", e =>
        {
            e.ConfigureConsumer<CastVoteConsumer>(context);
            e.ConcurrentMessageLimit = 40; // Znacznie więcej niż 4!
            e.PrefetchCount = 80;
        });
    });
});

const string serviceName = "AsynchronousVoting.Worker";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithMetrics(metrics => metrics
        .AddMeter("AsynchronousVoting.Worker.Metrics")
        .AddPrometheusHttpListener(options =>
        {
            options.UriPrefixes = new[]
            {
                $"http://+:{metricsPort}/"
            };
        }));

var host = builder.Build();
host.Run();