using System.Diagnostics.Metrics;

namespace AsynchronousVoting.Worker.Monitoring;

public static class VotingMetrics
{
    private static readonly Meter Meter = new("AsynchronousVoting.Worker.Metrics", "1.0.0");
    
    public static readonly Histogram<double> VoteRecordedEventQueueDelaySeconds =
        Meter.CreateHistogram<double>(
            name: "vote_recorded_event_queue_delay_seconds",
            unit: "s",
            description: "Time from VoteRecordedEvent publish to projection worker consume start");

    public static readonly Histogram<double> VoteDurableWriteDurationSeconds =
        Meter.CreateHistogram<double>(
            name: "vote_durable_write_duration_seconds",
            unit: "s",
            description: "Time from original request start to the vote being permanently recorded");

    public static readonly Histogram<double> VoteProjectionCompletionDurationSeconds =
        Meter.CreateHistogram<double>(
            name: "vote_projection_completion_duration_seconds",
            unit: "s",
            description: "Time from original request start to projection completion");

    public static readonly Histogram<double> VoteSubmissionQueueDelaySeconds =
        Meter.CreateHistogram<double>(
            name: "vote_submission_queue_delay_seconds",
            unit: "s",
            description: "Time from async vote submission publish to stage 1 worker consume start");

    public static readonly Counter<long> VotesProcessed =
        Meter.CreateCounter<long>(
            "votes_processed_total",
            unit: "votes",
            description: "Total number of votes processed by async worker");
}
