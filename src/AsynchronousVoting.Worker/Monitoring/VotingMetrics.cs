using System.Diagnostics.Metrics;

namespace AsynchronousVoting.Worker.Monitoring;

public static class VotingMetrics
{
    private static readonly Meter Meter = new("AsynchronousVoting.Worker.Metrics", "1.0.0");
    
    public static readonly Histogram<double> VoteProcessingDurationSeconds =
        Meter.CreateHistogram<double>(
            name: "vote_processing_duration_seconds",
            unit: "s",
            description: "End-to-end processing time of a vote in async worker (enqueue -> DB)");

    public static readonly Histogram<double> VoteQueueDelaySeconds =
        Meter.CreateHistogram<double>(
            name: "vote_queue_delay_seconds",
            unit: "s",
            description: "Time from broker send to worker consume start");

    public static readonly Histogram<double> VoteWorkerExecutionDurationSeconds =
        Meter.CreateHistogram<double>(
            name: "vote_worker_execution_duration_seconds",
            unit: "s",
            description: "Time from worker consume start to persisted completion");
    
    public static readonly Counter<long> VotesProcessed =
        Meter.CreateCounter<long>(
            "votes_processed_total",
            unit: "votes",
            description: "Total number of votes processed by async worker");

    public static readonly Histogram<double> VoteAcceptanceLatencySeconds =
        Meter.CreateHistogram<double>(
            name: "vote_acceptance_latency_seconds",
            unit: "s",
            description: "Time from HTTP request start to vote being saved (Stage 1 completion). Async-specific metric showing the fast-path decoupled from projection.");
}
