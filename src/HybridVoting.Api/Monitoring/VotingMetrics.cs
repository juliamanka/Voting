using System.Diagnostics.Metrics;

namespace HybridVoting.Api.Monitoring;

public static class VotingMetrics
{
    private static readonly Meter Meter = new("HybridVoting.Api.Metrics", "1.0.0");

    public static readonly Histogram<double> VoteProcessingDurationSeconds =
        Meter.CreateHistogram<double>(
            name: "vote_processing_duration_seconds",
            unit: "s",
            description: "End-to-end processing time of a vote in synchronous API");
}