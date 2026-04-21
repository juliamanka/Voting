using HybridVoting.Api.Monitoring;
using HybridVoting.Api.Requests;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Voting.Api.Common.RequestTiming;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;
using Voting.Domain.Enums;
using Voting.Infrastructure.Database;

namespace HybridVoting.Api.Controllers;

[ApiController]
[Route("api/vote")]
[EnableRateLimiting("votes-policy")]
public class VotesController : ControllerBase
{
    private readonly IVoteWriteService _voteWriteService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly VotingDbContext _dbContext;

    public VotesController(
        IVoteWriteService voteWriteService,
        IPublishEndpoint publishEndpoint,
        VotingDbContext dbContext)
    {
        _voteWriteService = voteWriteService;
        _publishEndpoint = publishEndpoint;
        _dbContext = dbContext;
    }

    [HttpPost]
    [ProducesResponseType(typeof(VoteResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CastVote(
        [FromBody] CastVoteRequest request,
        CancellationToken cancellationToken)
    {
        var voteRequest = new VoteRequest
        {
            PollId = request.PollId,
            PollOptionId = request.PollOptionId,
            UserId = request.UserId
        };

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var vote = await _voteWriteService.WriteVoteAsync(voteRequest, cancellationToken);
        var requestStartedAtUtc = RequestTimingContext.GetRequestStartedAtUtc(HttpContext, DateTime.UtcNow);
        await _publishEndpoint.Publish(
            new VoteRecordedEvent(
                vote.VoteId,
                vote.PollId,
                vote.PollOptionId,
                vote.UserId,
                vote.Timestamp,
                requestStartedAtUtc,
                DateTime.UtcNow),
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var responseLatency = RequestTimingContext.GetElapsedSinceRequestStart(HttpContext);
        VotingMetrics.VoteHttpResponseLatencySeconds.Record(responseLatency.TotalSeconds);
        
        return CreatedAtAction(nameof(CastVote), new { id = vote.VoteId }, new VoteResponse
        {
            VoteId = vote.VoteId,
            PollId = vote.PollId,
            Status = VoteStatus.Counted,
            Timestamp = vote.Timestamp,
            ServerProcessingTimeMs = (long)responseLatency.TotalMilliseconds
        });
    }
}
