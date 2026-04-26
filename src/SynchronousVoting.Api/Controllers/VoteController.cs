using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using SynchronousVoting.Api.Hubs;
using SynchronousVoting.Api.Monitoring;
using Voting.Api.Common.RequestTiming;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;

namespace SynchronousVoting.Api.Controllers;

[ApiController]
[Route("api/vote")]
[EnableRateLimiting("votes-policy")]
public class VoteController : ControllerBase
{
    private readonly IVotingService _votingService;
    private readonly IPollService _pollService;
    private readonly IHubContext<ResultsHub> _hubContext;

    public VoteController(
        IVotingService votingService,
        IPollService pollService,
        IHubContext<ResultsHub> hubContext)
    {
        _votingService = votingService;
        _pollService = pollService;
        _hubContext = hubContext;
    }
    
    [HttpPost]
    [ProducesResponseType(typeof(VoteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SubmitVote(
        [FromBody] VoteRequest request,
        CancellationToken cancellationToken)
    {
        var receipt = await _votingService.ProcessVoteAsync(request, cancellationToken);
        var updatedResults = await _pollService.GetVotesForPoll(request.PollId, cancellationToken);
        if (updatedResults is not null)
        {
            await _hubContext.Clients
                .Group(request.PollId.ToString())
                .SendAsync("PollResultsUpdated", updatedResults, cancellationToken);
        }

        var responseLatency = RequestTimingContext.GetElapsedSinceRequestStart(HttpContext);

        var tags = new KeyValuePair<string, object?>[]
        {
            new("architecture", "sync")
        };
        VotingMetrics.VoteProcessingDurationSeconds.Record(responseLatency.TotalSeconds, tags);
        VotingMetrics.VoteHttpResponseLatencySeconds.Record(responseLatency.TotalSeconds, tags);

        return CreatedAtAction(nameof(SubmitVote), new { id = receipt.VoteId }, receipt);
    }
}
