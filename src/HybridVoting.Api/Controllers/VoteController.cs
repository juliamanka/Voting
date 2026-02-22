using HybridVoting.Api.Monitoring;
using HybridVoting.Api.Requests;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Voting.Domain.Entities;
using Voting.Infrastructure.Database;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;

namespace HybridVoting.Api.Controllers;

[ApiController]
[Route("api/vote")]
[EnableRateLimiting("votes-policy")]
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
        var poll = await _dbContext.Polls
            .Include(p => p.Options)
            .FirstOrDefaultAsync(p => p.PollId == request.PollId, cancellationToken);

        if (poll is null)
            return NotFound($"Poll {request.PollId} not found.");

        var option = poll.Options.FirstOrDefault(o => o.PollOptionId == request.PollOptionId);
        if (option is null)
            return BadRequest($"Option {request.PollOptionId} does not belong to poll {request.PollId}.");

        await _voteNotifier.NotifyVoteAsync(request.PollId, request.PollOptionId, request.UserId, cancellationToken);
        
        return Accepted(new
        {
            pollId = request.PollId,
            optionId = request.PollOptionId,
            message = "Vote has been recorded and results will be updated shortly."
        });
    }
}