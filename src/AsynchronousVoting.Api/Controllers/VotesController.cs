using AsynchronousVoting.Api.Notifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;
using Voting.Domain.Enums;
using Voting.Infrastructure.Database;

namespace AsynchronousVoting.Api.Controllers;

[ApiController]
[Route("api/vote")]
[EnableRateLimiting("votes-policy")]
public class VotesController : ControllerBase
{
    private readonly IVoteSubmissionPublisher _voteSubmissionPublisher;
    private readonly VotingDbContext _dbContext;
    private readonly IVoteValidationService _voteValidationService;

    public VotesController(
        IVoteSubmissionPublisher voteSubmissionPublisher,
        VotingDbContext dbContext,
        IVoteValidationService voteValidationService)
    {
        _voteSubmissionPublisher = voteSubmissionPublisher;
        _dbContext = dbContext;
        _voteValidationService = voteValidationService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(VoteAcceptedResponse), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> CastVote([FromBody] VoteRequest request, CancellationToken ct)
    {
        await _voteValidationService.ValidateAsync(request, ct);

        var submissionId = await _voteSubmissionPublisher.SubmitVoteAsync(request.PollId, request.PollOptionId, request.UserId, ct);

        return Accepted(new VoteAcceptedResponse
        {
            SubmissionId = submissionId,
            Status = VoteStatus.Pending,
            Message = "Vote accepted for processing. Check the status endpoint for the final result.",
            PollId = request.PollId,
            PollOptionId = request.PollOptionId
        });
    }

    [HttpGet("status/{submissionId:guid}")]
    public async Task<ActionResult<VoteSubmissionStatusResponse>> GetVoteStatus(Guid submissionId, CancellationToken ct)
    {
        var submission = await _dbContext.VoteSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.SubmissionId == submissionId, ct);

        if (submission is null)
        {
            return NotFound();
        }

        return Ok(new VoteSubmissionStatusResponse
        {
            SubmissionId = submission.SubmissionId,
            PollId = submission.PollId,
            PollOptionId = submission.PollOptionId,
            UserId = submission.UserId,
            Architecture = submission.Architecture,
            Status = submission.Status,
            VoteId = submission.VoteId,
            FailureReason = submission.FailureReason,
            RequestStartedAtUtc = submission.RequestStartedAtUtc,
            AcceptedAtUtc = submission.AcceptedAtUtc,
            BrokerSentAtUtc = submission.BrokerSentAtUtc,
            WorkerStartedAtUtc = submission.WorkerStartedAtUtc,
            CompletedAtUtc = submission.CompletedAtUtc,
            HttpResponseLatencyMs = submission.HttpResponseLatencyMs,
            QueueDelayMs = submission.QueueDelayMs,
            WorkerExecutionLatencyMs = submission.WorkerExecutionLatencyMs,
            EndToEndLatencyMs = submission.EndToEndLatencyMs
        });
    }
}
