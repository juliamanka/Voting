using Microsoft.AspNetCore.Mvc;
using SynchronousVoting.Api.Monitoring;
using Voting.Api.Common.Contracts.Monitoring;

namespace SynchronousVoting.Api.Controllers;

[ApiController]
[Route("api/metrics/ux")]
public class UxMetricsController : ControllerBase
{
    [HttpPost("vote-latency")]
    public IActionResult ReportVoteLatency([FromBody] VoteLatencyDto dto,
        [FromQuery] string architecture = "sync")
    {
        var seconds = dto.LatencyMs / 1000.0;

        var tags = new KeyValuePair<string, object?>[]
        {
            new("architecture", architecture)
        };

        UxMetrics.UxVoteLatencySeconds.Record(seconds, tags);

        return Ok();
    }
}