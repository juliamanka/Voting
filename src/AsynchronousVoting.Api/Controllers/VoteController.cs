using Asynchronous.Api.Requests;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;

namespace AsynchronousVoting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VotesController : ControllerBase
{
    private readonly IVoteNotifier _voteNotifier;

    public VotesController(IVoteNotifier voteNotifier)
    {
        _voteNotifier = voteNotifier;
    }

    [HttpPost]
    public async Task<IActionResult> CastVote([FromBody] CastVoteRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (request.PollId == Guid.Empty || request.PollOptionId == Guid.Empty)
            return BadRequest("Invalid PollId or PollOptionId.");

        await _voteNotifier.NotifyVoteAsync(request.PollId, request.PollOptionId, request.UserId, ct);

        return Accepted(new
        {
            message = "Vote accepted for processing.",
            pollId = request.PollId,
            pollOptionId = request.PollOptionId
        });
    }
}