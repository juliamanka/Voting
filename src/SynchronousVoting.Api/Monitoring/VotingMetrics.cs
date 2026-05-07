using System.Diagnostics.Metrics;

namespace SynchronousVoting.Api.Monitoring;

public static class VotingMetrics
{
    private static readonly Meter Meter = new("SynchronousVoting.Api.Metrics", "1.0.0");

    public static readonly Histogram<double> VoteHttpResponseLatencySeconds =
        Meter.CreateHistogram<double>(
            name: "vote_http_response_latency_seconds",
            unit: "s",
            description: "HTTP response latency for POST /api/vote in synchronous API");

    public static readonly Histogram<double> VoteDurableWriteDurationSeconds =
        Meter.CreateHistogram<double>(
            name: "vote_durable_write_duration_seconds",
            unit: "s",
            description: "Time from HTTP request start to the vote being permanently recorded");

    public static readonly Histogram<double> VoteProjectionCompletionDurationSeconds =
        Meter.CreateHistogram<double>(
            name: "vote_projection_completion_duration_seconds",
            unit: "s",
            description: "Time from HTTP request start to projection completion");

    public static readonly Histogram<double> ResultsNotificationCompletionDurationSeconds =
        Meter.CreateHistogram<double>(
            name: "results_notification_completion_duration_seconds",
            unit: "s",
            description: "Time from HTTP request start to SignalR result notification send completion");

    public static readonly Histogram<double> SignalRSendDurationSeconds =
        Meter.CreateHistogram<double>(
            name: "signalr_send_duration_seconds",
            unit: "s",
            description: "Time spent sending the poll result notification through SignalR");
}
