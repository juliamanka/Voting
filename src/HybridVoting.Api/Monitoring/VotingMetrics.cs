using System.Diagnostics.Metrics;

namespace HybridVoting.Api.Monitoring;

public static class VotingMetrics
{
    private static readonly Meter Meter = new("HybridVoting.Api.Metrics", "1.0.0");

    public static readonly Histogram<double> VoteHttpResponseLatencySeconds =
        Meter.CreateHistogram<double>(
            name: "vote_http_response_latency_seconds",
            unit: "s",
            description: "HTTP response latency for POST /api/vote in hybrid API");
}
