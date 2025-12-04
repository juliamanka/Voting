using AsynchronousVoting.Worker.Messaging.Consumers;
using MassTransit;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Voting.Application;
using Voting.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

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
            options.UriPrefixes = new[] { "http://*:9184/" }; 
        }));


var host = builder.Build();
host.Run();