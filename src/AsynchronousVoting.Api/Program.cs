using System.Text.Json;
using Asynchronous.Api.Hubs;
using AsynchronousVoting.Api;
using AsynchronousVoting.Api.Messaging.Consumers;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Resources;
using Serilog;
using Voting.Api.Common;
using Voting.Application;
using Voting.Infrastructure;
using Prometheus;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Voting.Application.Interfaces;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

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

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddSignalR();

builder.Services.AddGlobalExceptionHandling();

const string serviceName = "AsynchronousVoting.Api";
var otlpEndpoint = builder.Configuration["OtlpExporter:Endpoint"];

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource(serviceName)
        .AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint!))
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint!))
        .AddConsoleExporter());

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

// MassTransit
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

        cfg.ReceiveEndpoint("vote-recorded-events", e =>
        {
            e.ConfigureConsumer<VoteRecordedEventConsumer>(context);
        });
    });
});

builder.Services.AddScoped<IVoteNotifier, AsyncVoteNotifier>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);

app.UseGlobalExceptionHandling();

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
app.UseHttpMetrics();
app.MapMetrics("/metrics");
app.MapControllers();
app.MapHub<ResultsHub>("/hubs/results");

app.Run();