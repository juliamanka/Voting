using System.Diagnostics;
using System.Diagnostics.Metrics;
using Hybrid.Worker.Monitoring;
using MassTransit;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;

namespace Hybrid.Worker.Messaging.Consumers;

public class VoteRecordedEventConsumer : IConsumer<VoteRecordedEvent>
{
    private readonly IVoteProjectionAndAuditService _voteProjectionAndAuditService;
    private readonly string _instanceId;

    public VoteRecordedEventConsumer(
        IVoteProjectionAndAuditService voteProjectionAndAuditService,
        IConfiguration configuration)
    {
        _voteProjectionAndAuditService = voteProjectionAndAuditService;
        _instanceId = configuration.GetValue<string?>("Worker:WorkerId") ?? "worker";
    }

    public async Task Consume(ConsumeContext<VoteRecordedEvent> context)
    {
        var workerStartedAtUtc = DateTime.UtcNow;
        var projection = await _voteProjectionAndAuditService.ApplyVoteAcceptedAsync(
            new Voting.Domain.Entities.VoteRecord
            {
                VoteId = context.Message.VoteId,
                PollId = context.Message.PollId,
                PollOptionId = context.Message.PollOptionId,
                UserId = context.Message.UserId,
                Timestamp = context.Message.VoteRecordedAtUtc
            },
            "hybrid",
            context.CancellationToken);

        var completedAtUtc = DateTime.UtcNow;
        var tags = new TagList
        {
            { "architecture", "hybrid" },
            { "worker_id", _instanceId },
            { "status", "Counted" }
        };

        VotingMetrics.VoteProcessingDurationSeconds.Record(
            Math.Max(0, (completedAtUtc - context.Message.RequestStartedAtUtc).TotalSeconds),
            tags);
        VotingMetrics.VoteQueueDelaySeconds.Record(
            Math.Max(0, (workerStartedAtUtc - context.Message.PublishedAtUtc).TotalSeconds),
            tags);
        VotingMetrics.VoteWorkerExecutionDurationSeconds.Record(
            Math.Max(0, (completedAtUtc - workerStartedAtUtc).TotalSeconds),
            tags);
        VotingMetrics.VotesProcessed.Add(1, tags);

        await context.Publish(new PollResultsUpdatedEvent(projection));
    }
}
