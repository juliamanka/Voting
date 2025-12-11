using System.Diagnostics.Metrics;

namespace HybridVoting.Api.Monitoring;

public static class UxMetrics
{
    private static readonly Meter Meter = new("HybridVoting.Api.Metrics", "1.0.0");

    public static readonly Histogram<double> UxVoteLatencySeconds =
        Meter.CreateHistogram<double>(
            name: "ux_vote_latency_seconds",
            unit: "s",
            description: "Time between user submitting a vote in UI and UI observing completion");
}