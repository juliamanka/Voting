using MassTransit;
using Microsoft.EntityFrameworkCore;
using Voting.Application.DTOs;
using Voting.Application.Exceptions;
using Voting.Application.Interfaces;
using Voting.Domain.Entities;
using Voting.Domain.Enums;
using Voting.Infrastructure.Database;
using ValidationException = FluentValidation.ValidationException;

namespace AsynchronousVoting.Worker.Messaging.Consumers;

public class VoteSaverConsumer : IConsumer<CastVoteCommand>
{
    private readonly record struct SubmissionFailure(VoteStatus Status, string FailureReason);

    private readonly VotingDbContext _dbContext;
    private readonly IVoteWriteService _voteWriteService;
    private readonly ILogger<VoteSaverConsumer> _logger;

    public VoteSaverConsumer(
        VotingDbContext dbContext,
        IVoteWriteService voteWriteService,
        ILogger<VoteSaverConsumer> logger)
    {
        _dbContext = dbContext;
        _voteWriteService = voteWriteService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CastVoteCommand> context)
    {
        var workerStartedAtUtc = DateTime.UtcNow;
        _logger.LogInformation("Received CastVoteCommand: PollId={PollId}, OptionId={OptionId}",
            context.Message.PollId, context.Message.PollOptionId);

        var msg = context.Message;
        var brokerSentAtUtc = (context.SentTime ?? msg.CreatedAtUtc).ToUniversalTime();

        var submission = await _dbContext.VoteSubmissions
            .FirstOrDefaultAsync(v => v.SubmissionId == msg.SubmissionId, context.CancellationToken);

        if (submission is null)
        {
            _logger.LogWarning("Vote submission {SubmissionId} not found. Skipping message.", msg.SubmissionId);
            return;
        }

        if (submission.Status != VoteStatus.Pending)
        {
            _logger.LogInformation(
                "Vote submission {SubmissionId} is already finalized with status {Status}.",
                submission.SubmissionId,
                submission.Status);
            return;
        }

        submission.BrokerSentAtUtc = brokerSentAtUtc;
        submission.WorkerStartedAtUtc = workerStartedAtUtc;
        submission.QueueDelayMs = Math.Max(0L, (long)(workerStartedAtUtc - brokerSentAtUtc).TotalMilliseconds);

        var voteRequest = new VoteRequest
        {
            PollId = msg.PollId,
            PollOptionId = msg.PollOptionId,
            UserId = msg.UserId
        };

        VoteRecord? vote = null;

        try
        {
            vote = await _voteWriteService.WriteVoteAsync(voteRequest, context.CancellationToken);

            var savedAtUtc = DateTime.UtcNow;

            submission.Status = VoteStatus.Counted;
            submission.VoteId = vote.VoteId;
            submission.CompletedAtUtc = savedAtUtc;
            submission.WorkerExecutionLatencyMs = Math.Max(0L, (long)(savedAtUtc - workerStartedAtUtc).TotalMilliseconds);
            submission.EndToEndLatencyMs = Math.Max(0L, (long)(savedAtUtc - msg.RequestStartedAtUtc).TotalMilliseconds);

            await context.Publish(
                new VoteRecordedEvent(
                    vote.VoteId,
                    vote.PollId,
                    vote.PollOptionId,
                    vote.UserId,
                    vote.Timestamp,
                    msg.RequestStartedAtUtc,
                    savedAtUtc)
                {
                    BrokerSentAtUtc = brokerSentAtUtc,
                    Stage1WorkerStartedAtUtc = workerStartedAtUtc
                },
                context.CancellationToken);

            await _dbContext.SaveChangesAsync(context.CancellationToken);
        }
        catch (Exception ex)
        {
            var failure = MapSubmissionFailure(ex, vote);
            if (failure is null)
            {
                _logger.LogError(ex, "Vote submission {SubmissionId} failed unexpectedly.", submission.SubmissionId);
                failure = new SubmissionFailure(VoteStatus.Failed, ex.Message);
            }

            var completedAtUtc = DateTime.UtcNow;
            submission.Status = failure.Value.Status;
            submission.FailureReason = failure.Value.FailureReason;
            submission.CompletedAtUtc = completedAtUtc;
            submission.WorkerExecutionLatencyMs = Math.Max(0L, (long)(completedAtUtc - workerStartedAtUtc).TotalMilliseconds);
            submission.EndToEndLatencyMs = Math.Max(0L, (long)(completedAtUtc - msg.RequestStartedAtUtc).TotalMilliseconds);
            await _dbContext.SaveChangesAsync(context.CancellationToken);
        }
    }

    private SubmissionFailure? MapSubmissionFailure(Exception exception, VoteRecord? vote)
    {
        switch (exception)
        {
            case DuplicateVoteException duplicateVoteException:
                return new SubmissionFailure(VoteStatus.Duplicate, duplicateVoteException.Message);

            case DbUpdateException dbUpdateException when dbUpdateException.IsUniqueConstraintViolation():
                if (vote is not null)
                {
                    _dbContext.Entry(vote).State = EntityState.Detached;
                }
                return new SubmissionFailure(VoteStatus.Duplicate, "Vote already exists.");

            case ValidationException validationException:
                return new SubmissionFailure(VoteStatus.Rejected, validationException.Message);

            case NotFoundException notFoundException:
                return new SubmissionFailure(VoteStatus.Rejected, notFoundException.Message);

            case PollInactiveException pollInactiveException:
                return new SubmissionFailure(VoteStatus.Rejected, pollInactiveException.Message);

            case IneligibleVoterException ineligibleVoterException:
                return new SubmissionFailure(VoteStatus.Rejected, ineligibleVoterException.Message);

            default:
                return null;
        }
    }
}
