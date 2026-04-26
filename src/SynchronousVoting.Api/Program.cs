using System.Reflection;
using System.Threading.RateLimiting;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Voting.Api.Common;
using Voting.Api.Common.RequestTiming;
using Voting.Application;
using Voting.Infrastructure;
using Voting.Infrastructure.Database;
using SynchronousVoting.Api.Hubs;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build())
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ApplicationName", Assembly.GetExecutingAssembly().GetName().Name)
    .Enrich.WithEnvironmentName()
    .WriteTo.Console()
    .WriteTo.OpenTelemetry(opts =>
    {
        opts.Endpoint = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build()
            .GetValue<string>("OtlpExporter:Endpoint");
    })
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
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
                    PermitLimit = 400,
                    Window = TimeSpan.FromSeconds(1),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                }));
    });

    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSignalR();

    builder.Services.AddControllers();

    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddGlobalExceptionHandling();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Synchronous Voting API", Version = "v1" });
    });

    const string serviceName = "SynchronousVoting.Api";

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
            .AddMeter("SynchronousVoting.Api.Metrics")
            .AddView("vote_processing_duration_seconds", new ExplicitBucketHistogramConfiguration
            {
                Boundaries = new[]
                {
                    0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5,
                    1, 2, 5, 10, 20, 30, 45, 60, 90, 120, 180, 240, 300
                }
            })
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
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
        .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!,
                      name: "database",
                      tags: new[] { "ready" });

    var app = builder.Build();

    app.ApplyMigrations();

    app.UseRequestTiming();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.UseSerilogRequestLogging();
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });

    app.UseGlobalExceptionHandling();
    app.UseRateLimiter();
    app.UseAuthorization();
    app.UseCors("AllowFrontend");
    app.UseOpenTelemetryPrometheusScrapingEndpoint();
    app.MapControllers();
    app.MapHub<ResultsHub>("/hubs/results");
    app.MapHub<ResultsHub>("/hubs/votes");

    if (app.Environment.IsDevelopment())
    {
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<VotingDbContext>();
            dbContext.Database.EnsureCreated();
        }
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application could not be run.");
}
finally
{
    Log.CloseAndFlush();
}
