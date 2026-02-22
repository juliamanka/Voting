using System.Text.Json;
using System.Threading.RateLimiting;
using HybridVoting.Api.Hubs;
using HybridVoting.Api.Messaging.Consumers;
using HybridVoting.Api.Notifiers;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Voting.Api.Common;
using Voting.Application;
using Voting.Application.Interfaces;
using Voting.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

const string CorsPolicy = "AllowFrontend";

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy
            .WithOrigins("http://localhost:4200") 
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

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

builder.Services.AddSignalR();
builder.Services.AddGlobalExceptionHandling();

const string serviceName = "HybridVoting.Api";

var otel = builder.Services.AddOpenTelemetry();
otel.ConfigureResource(resource =>
    resource.AddService(serviceName));

otel.WithMetrics(metrics =>
{
    metrics
        .AddAspNetCoreInstrumentation()
        .AddMeter("HybridVoting.Api.Metrics") 
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
    .AddMySql(
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection"),
        name: "mysql");


var rabbitSection = builder.Configuration.GetSection("RabbitMq");
if (!rabbitSection.Exists())
    throw new InvalidOperationException("No section 'RabbitMq' in appsettings.json.");

var rabbitHost = rabbitSection["Host"] ?? throw new InvalidOperationException("RabbitMq:Host is missing");
var rabbitUser = rabbitSection["Username"] ?? throw new InvalidOperationException("RabbitMq:Username is missing");
var rabbitPass = rabbitSection["Password"] ?? throw new InvalidOperationException("RabbitMq:Password is missing");

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<VoteRecordedEventConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        cfg.ReceiveEndpoint("hybrid-vote-recorded-events", e =>
        {
            e.ConfigureConsumer<VoteRecordedEventConsumer>(context);
            
            // LIMIT RÓWNOLEGŁYCH WIADOMOŚCI (workerów)
            e.ConcurrentMessageLimit = 4;     // np. max 8 "workerów" równolegle
            
            // LIMIT PREFETCHU z RabbitMQ (ile niepotwierdzonych wiadomości naraz)
            e.PrefetchCount = 8;             // e.g. 2x concurrency
            
        });
    });
});

builder.Services.AddScoped<IVoteNotifier, HybridVoteNotifier>();

var app = builder.Build();

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