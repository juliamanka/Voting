using System.Reflection;
using System.Text.Json;
using AsynchronousVoting.Api.Hubs;
using AsynchronousVoting.Api.Messaging.Consumers;
using AsynchronousVoting.Api.Notifiers;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Voting.Api.Common;
using Voting.Api.Common.RequestTiming;
using Voting.Application;
using Voting.Application.Interfaces;
using Voting.Infrastructure;
using System.Threading.RateLimiting;
using Voting.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build())
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ApplicationName", Assembly.GetExecutingAssembly().GetName().Name)
    .WriteTo.Console()
    .CreateLogger();

const string CorsPolicy = "AllowFrontend";

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy
            .WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Host.UseSerilog();

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("votes-policy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: "global",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120, // Więcej niż testowane 100 RPS
                Window = TimeSpan.FromSeconds(1),
                QueueLimit = 0,    // Lepiej odrzucać od razu niż buforować na API
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddHttpContextAccessor();

builder.Services.AddSignalR();

builder.Services.AddGlobalExceptionHandling();

const string serviceName = "AsynchronousVoting.Api";

var otel = builder.Services.AddOpenTelemetry();
otel.ConfigureResource(resource =>
    resource.AddService(serviceName));

otel.WithMetrics(metrics =>
{
    metrics
        .AddAspNetCoreInstrumentation()
        .AddView("http.server.request.duration", new ExplicitBucketHistogramConfiguration
        {
            Boundaries = new[]
            {
                0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5,
                1, 2, 5, 10, 20, 30, 45, 60, 90, 120, 180, 240, 300
            }
        })
        .AddMeter("AsynchronousVoting.Api.Metrics")
        .AddView("vote_http_response_latency_seconds", new ExplicitBucketHistogramConfiguration
        {
            Boundaries = new[]
            {
                0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5,
                1, 2, 5, 10, 20, 30, 45, 60, 90, 120, 180, 240, 300
            }
        })
        .AddView("ux_vote_latency_seconds", new ExplicitBucketHistogramConfiguration
        {
            Boundaries = new[]
            {
                0.01, 0.02, 0.05, 0.1, 0.2, 0.5,
                1, 2, 5, 10, 15, 20, 30, 45, 60, 90, 120
            }
        })
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddPrometheusExporter();
});
otel.WithTracing(tracing =>
{
    tracing
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddHttpClientInstrumentation();
});

builder.Services.AddHealthChecks()
    .AddSqlServer(
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection"),
        name: "sqlserver");

var rabbitSection = builder.Configuration.GetSection("RabbitMq");
if (!rabbitSection.Exists())
    throw new InvalidOperationException("No section 'RabbitMq' in appsettings.json.");

var rabbitHost = rabbitSection["Host"] ?? throw new InvalidOperationException("RabbitMq:Host is missing");
var rabbitUser = rabbitSection["Username"] ?? throw new InvalidOperationException("RabbitMq:Username is missing");
var rabbitPass = rabbitSection["Password"] ?? throw new InvalidOperationException("RabbitMq:Password is missing");

// MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PollResultsUpdatedEventConsumer>();

    x.AddEntityFrameworkOutbox<VotingDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
        o.DisableInboxCleanupService(); // API zazwyczaj tylko wysyła, nie potrzebuje cleanupu inboxa
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        cfg.ReceiveEndpoint("async-poll-results-updated-events",
            e =>
            {
                e.UseEntityFrameworkOutbox<VotingDbContext>(context);
                e.ConfigureConsumer<PollResultsUpdatedEventConsumer>(context);
                e.ConcurrentMessageLimit = 4;
                e.PrefetchCount = 8;
            });
    });
});

builder.Services.AddScoped<IVoteNotifier, AsyncVoteNotifier>();

var app = builder.Build();

app.ApplyMigrations();

app.UseRequestTiming();
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);

app.UseGlobalExceptionHandling();

app.UseRateLimiter();

app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                error = e.Value.Exception?.Message
            })
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
});
app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.MapControllers();
app.MapHub<ResultsHub>("/hubs/results");

app.Run();
