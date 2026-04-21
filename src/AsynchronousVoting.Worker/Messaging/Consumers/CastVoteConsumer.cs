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
    private readonly VotingDbContext _dbContext;
    private readonly IVoteValidationService _voteValidationService;
    private readonly IVoteProjectionAndAuditService _voteProjectionAndAuditService;
    private readonly ILogger<CastVoteConsumer> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _instanceId;

    public CastVoteConsumer(
        VotingDbContext dbContext,
        IVoteValidationService voteValidationService,
        IVoteProjectionAndAuditService voteProjectionAndAuditService,
        ILogger<CastVoteConsumer> logger,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _voteValidationService = voteValidationService;
        _voteProjectionAndAuditService = voteProjectionAndAuditService;
        _logger = logger;
        _configuration = configuration;
        _instanceId = _configuration.GetValue<string?>("Worker:WorkerId");
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
            await _voteValidationService.ValidateAsync(voteRequest, context.CancellationToken);

            vote = new VoteRecord
            {
                VoteId = Guid.NewGuid(),
                PollId = msg.PollId,
                PollOptionId = msg.PollOptionId,
                UserId = msg.UserId,
                Status = VoteStatus.Counted,
                Timestamp = DateTime.UtcNow
            };

            _dbContext.Votes.Add(vote);
            submission.Status = VoteStatus.Counted;
            submission.VoteId = vote.VoteId;
            submission.FailureReason = null;
            await _dbContext.SaveChangesAsync(context.CancellationToken);

            var projection = await _voteProjectionAndAuditService.ApplyVoteAcceptedAsync(
                vote,
                "async",
                context.CancellationToken);

            var completedAtUtc = DateTime.UtcNow;
            await FinalizeSubmissionAsync(
                submission,
                VoteStatus.Counted,
                msg.RequestStartedAtUtc,
                consumeStartedAtUtc,
                completedAtUtc,
                context.CancellationToken,
                vote.VoteId);

            RecordOutcomeMetrics("async", VoteStatus.Counted, msg.RequestStartedAtUtc, brokerSentAtUtc, consumeStartedAtUtc, completedAtUtc, _instanceId);

            await context.Publish(new PollResultsUpdatedEvent(projection));
        }
        catch (DuplicateVoteException ex)
        {
            var completedAtUtc = await MarkSubmissionAsync(
                submission,
                VoteStatus.Duplicate,
                ex.Message,
                msg.RequestStartedAtUtc,
                consumeStartedAtUtc,
                context.CancellationToken);
            RecordOutcomeMetrics("async", VoteStatus.Duplicate, msg.RequestStartedAtUtc, brokerSentAtUtc, consumeStartedAtUtc, completedAtUtc, _instanceId);
        }
        catch (ValidationException ex)
        {
            var completedAtUtc = await MarkSubmissionAsync(
                submission,
                VoteStatus.Rejected,
                ex.Message,
                msg.RequestStartedAtUtc,
                consumeStartedAtUtc,
                context.CancellationToken);
            RecordOutcomeMetrics("async", VoteStatus.Rejected, msg.RequestStartedAtUtc, brokerSentAtUtc, consumeStartedAtUtc, completedAtUtc, _instanceId);
        }
        catch (NotFoundException ex)
        {
            var completedAtUtc = await MarkSubmissionAsync(
                submission,
                VoteStatus.Rejected,
                ex.Message,
                msg.RequestStartedAtUtc,
                consumeStartedAtUtc,
                context.CancellationToken);
            RecordOutcomeMetrics("async", VoteStatus.Rejected, msg.RequestStartedAtUtc, brokerSentAtUtc, consumeStartedAtUtc, completedAtUtc, _instanceId);
        }
        catch (PollInactiveException ex)
        {
            var completedAtUtc = await MarkSubmissionAsync(
                submission,
                VoteStatus.Rejected,
                ex.Message,
                msg.RequestStartedAtUtc,
                consumeStartedAtUtc,
                context.CancellationToken);
            RecordOutcomeMetrics("async", VoteStatus.Rejected, msg.RequestStartedAtUtc, brokerSentAtUtc, consumeStartedAtUtc, completedAtUtc, _instanceId);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            if (vote is not null)
            {
                _dbContext.Entry(vote).State = EntityState.Detached;
            }

            var completedAtUtc = await MarkSubmissionAsync(
                submission,
                VoteStatus.Duplicate,
                "Vote already exists.",
                msg.RequestStartedAtUtc,
                consumeStartedAtUtc,
                context.CancellationToken);
            RecordOutcomeMetrics("async", VoteStatus.Duplicate, msg.RequestStartedAtUtc, brokerSentAtUtc, consumeStartedAtUtc, completedAtUtc, _instanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vote submission {SubmissionId} failed unexpectedly.", submission.SubmissionId);
            var completedAtUtc = await MarkSubmissionAsync(
                submission,
                VoteStatus.Failed,
                ex.Message,
                msg.RequestStartedAtUtc,
                consumeStartedAtUtc,
                context.CancellationToken);
            RecordOutcomeMetrics("async", VoteStatus.Failed, msg.RequestStartedAtUtc, brokerSentAtUtc, consumeStartedAtUtc, completedAtUtc, _instanceId);
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
        submission.Status = status;
        submission.VoteId = null;
        submission.FailureReason = failureReason;
        await _dbContext.SaveChangesAsync(cancellationToken);
        var completedAtUtc = DateTime.UtcNow;
        await FinalizeSubmissionAsync(
            submission,
            status,
            requestStartedAtUtc,
            workerStartedAtUtc,
            completedAtUtc,
            cancellationToken,
            null,
            failureReason);
        return completedAtUtc;
    }

    private async Task FinalizeSubmissionAsync(
        VoteSubmission submission,
        VoteStatus status,
        DateTime requestStartedAtUtc,
        DateTime workerStartedAtUtc,
        DateTime completedAtUtc,
        CancellationToken cancellationToken,
        Guid? voteId = null,
        string? failureReason = null)
    {
        submission.Status = status;
        submission.VoteId = voteId;
        submission.FailureReason = failureReason;
        submission.CompletedAtUtc = completedAtUtc;
        submission.WorkerExecutionLatencyMs = Math.Max(0L, (long)(completedAtUtc - workerStartedAtUtc).TotalMilliseconds);
        submission.EndToEndLatencyMs = Math.Max(0L, (long)(completedAtUtc - requestStartedAtUtc).TotalMilliseconds);
        await _dbContext.SaveChangesAsync(cancellationToken);
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
