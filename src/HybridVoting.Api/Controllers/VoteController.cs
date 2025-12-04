using HybridVoting.Api.Monitoring;
using HybridVoting.Api.Requests;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Voting.Domain.Entities;
using Voting.Infrastructure.Database;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;

namespace HybridVoting.Api.Controllers;

[ApiController]
[Route("api/vote")]
public class VotesController : ControllerBase
{
    private readonly VotingDbContext _dbContext;
    private readonly IVoteNotifier _voteNotifier;

    public VotesController(
        VotingDbContext dbContext,
        IVoteNotifier voteNotifier)
    {
        _dbContext = dbContext;
        _voteNotifier = voteNotifier;
    }

    [HttpPost]
    public async Task<IActionResult> CastVote(
        [FromBody] CastVoteRequest request,
        CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;

        var poll = await _dbContext.Polls
            .Include(p => p.Options)
            .FirstOrDefaultAsync(p => p.PollId == request.PollId, cancellationToken);

        if (poll is null)
            return NotFound($"Poll {request.PollId} not found.");

        var option = poll.Options.FirstOrDefault(o => o.PollOptionId == request.PollOptionId);
        if (option is null)
            return BadRequest($"Option {request.PollOptionId} does not belong to poll {request.PollId}.");

        var vote = new VoteRecord
        {
            VoteId = Guid.NewGuid(),
            PollId = request.PollId,
            PollOptionId = request.PollOptionId,
            UserId = request.UserId,
            Timestamp = DateTime.UtcNow
        };

        _dbContext.Votes.Add(vote);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _voteNotifier.NotifyVoteAsync(request.PollId, request.PollOptionId, request.UserId, cancellationToken);

        var duration = DateTime.UtcNow - start;

        var tags = new KeyValuePair<string, object?>[]
        {
            new("architecture", "hybrid")
        };
        VotingMetrics.VoteProcessingDurationSeconds.Record(duration.TotalSeconds, tags);
        
        return Accepted(new
        {
            pollId = request.PollId,
            optionId = request.PollOptionId,
            message = "Vote has been recorded and results will be updated shortly."
        });
    }
}