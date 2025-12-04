using System.Diagnostics.Metrics;

namespace AsynchronousVoting.Worker.Monitoring;

public static class VotingMetrics
{
    private static readonly Meter Meter = new("AsynchronousVoting.Worker.Metrics", "1.0.0");

    /// <summary>
    /// Czas przetwarzania głosu w architekturze asynchronicznej:
    /// od momentu enqueue (EnqueuedAtUtc) do zakończenia zapisu w bazie.
    /// Jednostka: sekundy.
    /// </summary>
    public static readonly Histogram<double> VoteProcessingDurationSeconds =
        Meter.CreateHistogram<double>(
            name: "vote_processing_duration_seconds",
            unit: "s",
            description: "End-to-end processing time of a vote in async worker (enqueue -> DB)");
}