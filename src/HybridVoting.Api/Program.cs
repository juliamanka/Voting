using System.Text.Json;
using HybridVoting.Api;
using HybridVoting.Api.Hubs;
using HybridVoting.Api.Messaging.Consumers;
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

// ---------------------------
// Serilog
// ---------------------------
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// ---------------------------
// CORS
// ---------------------------
const string CorsPolicy = "AllowFrontend";

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy
            .WithOrigins("http://localhost:4200") // albo z configa: builder.Configuration["FrontendUrl"]
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ---------------------------
// MVC, Swagger
// ---------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---------------------------
// Application / Infrastructure
// ---------------------------
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// ---------------------------
// SignalR + global exception handling
// ---------------------------
builder.Services.AddSignalR();
builder.Services.AddGlobalExceptionHandling();

// ---------------------------
// OpenTelemetry
// ---------------------------
const string serviceName = "HybridVoting.Api";

var otel = builder.Services.AddOpenTelemetry();
otel.ConfigureResource(resource =>
    resource.AddService(serviceName));

otel.WithMetrics(metrics =>
{
    metrics
        .AddAspNetCoreInstrumentation()
        .AddMeter("Hybrid.Api.Metrics") 
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

// ---------------------------
// HealthChecks
// ---------------------------
builder.Services.AddHealthChecks()
    .AddMySql(
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection"),
        name: "mysql");

// ---------------------------
// RabbitMQ / MassTransit
// ---------------------------
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
        });
    });
});

// ---------------------------
// Hybrydowa implementacja IVoteNotifier
// ---------------------------
builder.Services.AddScoped<IVoteNotifier, HybridVoteNotifier>();

var app = builder.Build();

// ---------------------------
// Middleware pipeline
// ---------------------------
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseCors(CorsPolicy);

app.UseGlobalExceptionHandling();

app.UseAuthorization();

// Health checks – JSON output (ładnie pod Grafanę / K8s probes itd.)
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

// Prometheus metrics
app.UseOpenTelemetryPrometheusScrapingEndpoint(); 

// API + SignalR
app.MapControllers();
app.MapHub<ResultsHub>("/hubs/results");

app.Run();