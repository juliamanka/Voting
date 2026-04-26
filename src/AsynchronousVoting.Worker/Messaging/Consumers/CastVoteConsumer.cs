using System.Diagnostics;
using System.Diagnostics.Metrics;
using AsynchronousVoting.Worker.Monitoring;
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

public class CastVoteConsumer : IConsumer<CastVoteCommand>
{
    private readonly record struct SubmissionFailure(VoteStatus Status, string FailureReason);

    private readonly VotingDbContext _dbContext;
    private readonly IVoteWriteService _voteWriteService;
    private readonly ILogger<CastVoteConsumer> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _instanceId;
    private readonly IVoteProjectionAndAuditService _voteProjectionAndAuditService;

    public CastVoteConsumer(
        VotingDbContext dbContext,
        IVoteWriteService voteWriteService,
        ILogger<CastVoteConsumer> logger,
        IConfiguration configuration,
        IVoteProjectionAndAuditService voteProjectionAndAuditService)
    {
        _dbContext = dbContext;
        _voteWriteService = voteWriteService;
        _logger = logger;
        _configuration = configuration;
        _instanceId = _configuration.GetValue<string?>("Worker:WorkerId") ?? "worker";
        _voteProjectionAndAuditService = voteProjectionAndAuditService;
    }

    public async Task Consume(ConsumeContext<CastVoteCommand> context)
    {
        var consumeStartedAtUtc = DateTime.UtcNow;
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

        InitializeSubmissionTiming(submission, brokerSentAtUtc, consumeStartedAtUtc);

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
            
            var results = await _voteProjectionAndAuditService.ApplyVoteAcceptedAsync(
                vote,
                "async",
                context.CancellationToken);

            var completedAtUtc = DateTime.UtcNow;

            CompleteSubmission(
                submission,
                VoteStatus.Counted,
                msg.RequestStartedAtUtc,
                consumeStartedAtUtc,
                completedAtUtc,
                vote.VoteId);

            await context.Publish(
                new PollResultsUpdatedEvent(results), 
                context.CancellationToken);

            RecordOutcomeMetrics("async", VoteStatus.Counted, msg.RequestStartedAtUtc, brokerSentAtUtc, consumeStartedAtUtc, completedAtUtc, _instanceId);
            
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

            var completedAtUtc = await MarkSubmissionAsync(
                submission,
                failure.Value.Status,
                failure.Value.FailureReason,
                msg.RequestStartedAtUtc,
                consumeStartedAtUtc,
                context.CancellationToken);
            RecordOutcomeMetrics("async", failure.Value.Status, msg.RequestStartedAtUtc, brokerSentAtUtc, consumeStartedAtUtc, completedAtUtc, _instanceId);
        }
    }

    private static void InitializeSubmissionTiming(
        VoteSubmission submission,
        DateTime brokerSentAtUtc,
        DateTime workerStartedAtUtc)
    {
        submission.BrokerSentAtUtc = brokerSentAtUtc;
        submission.WorkerStartedAtUtc = workerStartedAtUtc;
        submission.QueueDelayMs = Math.Max(0L, (long)(workerStartedAtUtc - brokerSentAtUtc).TotalMilliseconds);
    }

    private async Task<DateTime> MarkSubmissionAsync(
        VoteSubmission submission,
        VoteStatus status,
        string failureReason,
        DateTime requestStartedAtUtc,
        DateTime workerStartedAtUtc,
        CancellationToken cancellationToken)
    {
        var completedAtUtc = DateTime.UtcNow;
        CompleteSubmission(
            submission,
            status,
            requestStartedAtUtc,
            workerStartedAtUtc,
            completedAtUtc,
            null,
            failureReason);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return completedAtUtc;
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

    private static void CompleteSubmission(
        VoteSubmission submission,
        VoteStatus status,
        DateTime requestStartedAtUtc,
        DateTime workerStartedAtUtc,
        DateTime completedAtUtc,
        Guid? voteId = null,
        string? failureReason = null)
    {
        submission.Status = status;
        submission.VoteId = voteId;
        submission.FailureReason = failureReason;
        submission.CompletedAtUtc = completedAtUtc;
        submission.WorkerExecutionLatencyMs = Math.Max(0L, (long)(completedAtUtc - workerStartedAtUtc).TotalMilliseconds);
        submission.EndToEndLatencyMs = Math.Max(0L, (long)(completedAtUtc - requestStartedAtUtc).TotalMilliseconds);
    }

    private static void RecordOutcomeMetrics(
        string architecture,
        VoteStatus status,
        DateTime requestStartedAtUtc,
        DateTime brokerSentAtUtc,
        DateTime workerStartedAtUtc,
        DateTime completedAtUtc,
        string instanceId)
    {
        var endToEndDuration = completedAtUtc - requestStartedAtUtc;
        var queueDelay = workerStartedAtUtc - brokerSentAtUtc;
        var workerExecution = completedAtUtc - workerStartedAtUtc;
        var tags = new TagList
        {
            { "architecture", architecture },
            { "worker_id", instanceId },
            { "status", status.ToString() }
        };

        VotingMetrics.VoteProcessingDurationSeconds.Record(endToEndDuration.TotalSeconds, tags);
        VotingMetrics.VoteQueueDelaySeconds.Record(Math.Max(0, queueDelay.TotalSeconds), tags);
        VotingMetrics.VoteWorkerExecutionDurationSeconds.Record(Math.Max(0, workerExecution.TotalSeconds), tags);
        VotingMetrics.VotesProcessed.Add(1, tags);
    }
}
