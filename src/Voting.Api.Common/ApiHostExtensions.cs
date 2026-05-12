using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Voting.Application.Options;

namespace Voting.Api.Common;

public static class ApiHostExtensions
{
    private static readonly double[] LongLatencyBuckets =
    {
        0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5,
        1, 2, 5, 10, 20, 30, 45, 60, 90, 120, 180, 240, 300
    };

    private static readonly double[] QueueLatencyBuckets =
    {
        0.001, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5,
        1, 2, 5, 10, 20, 30, 45, 60, 90, 120, 180, 240, 300
    };

    private static readonly double[] SignalRBuckets =
    {
        0.001, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5,
        1, 2, 5, 10, 20, 30
    };

    private static readonly double[] UxLatencyBuckets =
    {
        0.01, 0.02, 0.05, 0.1, 0.2, 0.5,
        1, 2, 5, 10, 15, 20, 30, 45, 60, 90, 120
    };

    public static IServiceCollection AddVotingCors(
        this IServiceCollection services,
        string policyName = "AllowFrontend")
    {
        services.AddCors(options =>
        {
            options.AddPolicy(policyName, policy =>
            {
                policy
                    .WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }

    public static IServiceCollection AddVoteRateLimiter(
        this IServiceCollection services,
        int permitLimit = 400)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy("votes-policy", _ =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: "global",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));
        });

        return services;
    }

    public static IServiceCollection AddProjectionDelayOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ProjectionOptions>(options =>
        {
            options.DelayMs = configuration.GetValue<int?>("Chaos:ProjectionDelayMs")
                              ?? ReadPositiveIntEnvironmentVariable("CHAOS_PROJECTION_DELAY_MS");
        });

        return services;
    }

    public static IServiceCollection AddVotingOpenTelemetry(
        this IServiceCollection services,
        string serviceName,
        string meterName)
    {
        var otel = services.AddOpenTelemetry();
        otel.ConfigureResource(resource => resource.AddService(serviceName));

        otel.WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddView("http.server.request.duration", Histogram(LongLatencyBuckets))
                .AddMeter(meterName)
                .AddView("vote_http_response_latency_seconds", Histogram(LongLatencyBuckets))
                .AddView("vote_durable_write_duration_seconds", Histogram(LongLatencyBuckets))
                .AddView("vote_projection_completion_duration_seconds", Histogram(LongLatencyBuckets))
                .AddView("results_notification_completion_duration_seconds", Histogram(LongLatencyBuckets))
                .AddView("poll_results_updated_event_queue_delay_seconds", Histogram(QueueLatencyBuckets))
                .AddView("signalr_send_duration_seconds", Histogram(SignalRBuckets))
                .AddView("ux_vote_latency_seconds", Histogram(UxLatencyBuckets))
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

        return services;
    }

    public static IHealthChecksBuilder AddVotingSqlHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        return services
            .AddHealthChecks()
            .AddSqlServer(
                connectionString: connectionString,
                name: "sqlserver");
    }

    public static IEndpointConventionBuilder MapVotingJsonHealthChecks(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/health")
    {
        return endpoints.MapHealthChecks(pattern, new HealthCheckOptions
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
    }

    private static ExplicitBucketHistogramConfiguration Histogram(double[] buckets) =>
        new() { Boundaries = buckets };

    private static int ReadPositiveIntEnvironmentVariable(string name)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return int.TryParse(raw, out var value) && value > 0 ? value : 0;
    }
}
