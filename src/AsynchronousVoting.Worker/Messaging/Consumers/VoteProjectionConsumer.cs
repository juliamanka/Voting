using System.Diagnostics.Metrics;
using AsynchronousVoting.Worker.Monitoring;
using MassTransit;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;
using Voting.Domain.Entities;
using Voting.Domain.Enums;

namespace AsynchronousVoting.Worker.Messaging.Consumers;

public class VoteProjectionConsumer : IConsumer<VoteRecordedEvent>
{
    private readonly IVoteProjectionAndAuditService _voteProjectionAndAuditService;
    private readonly ILogger<VoteProjectionConsumer> _logger;
    private readonly string _instanceId;

    public VoteProjectionConsumer(
        IVoteProjectionAndAuditService voteProjectionAndAuditService,
        ILogger<VoteProjectionConsumer> logger,
        IConfiguration configuration)
    {
        _voteProjectionAndAuditService = voteProjectionAndAuditService;
        _logger = logger;
        _instanceId = configuration.GetValue<string?>("Worker:WorkerId") ?? "worker";
    }

    public async Task Consume(ConsumeContext<VoteRecordedEvent> context)
    {
        var stage2WorkerStartedAtUtc = DateTime.UtcNow;
        var msg = context.Message;
        _logger.LogInformation("Received VoteRecordedEvent: VoteId={VoteId}", msg.VoteId);

        var vote = new VoteRecord
        {
            VoteId = msg.VoteId,
            PollId = msg.PollId,
            PollOptionId = msg.PollOptionId,
            UserId = msg.UserId,
            Timestamp = msg.VoteRecordedAtUtc,
            Status = VoteStatus.Counted
        };

        var results = await _voteProjectionAndAuditService.ApplyVoteAcceptedAsync(
            vote,
            "async",
            context.CancellationToken);

        var completedAtUtc = DateTime.UtcNow;

        var tags = new TagList
        {
            { "architecture", "async" },
            { "worker_id", _instanceId },
            { "status", VoteStatus.Counted.ToString() }
        };

        // Comparable with hybrid: requestStart → projection done (total pipeline latency).
        VotingMetrics.VoteProcessingDurationSeconds.Record(
            Math.Max(0, (completedAtUtc - msg.RequestStartedAtUtc).TotalSeconds), tags);

        // Comparable with hybrid: publishedAt → stage2 consume start
        // = time VoteRecordedEvent waited in async-vote-recorded-events queue.
        // Hybrid measures the same span (publishedAt → workerStart) for its projection queue.
        VotingMetrics.VoteQueueDelaySeconds.Record(
            Math.Max(0, (stage2WorkerStartedAtUtc - msg.PublishedAtUtc).TotalSeconds), tags);

        // Comparable with hybrid: stage2 consume start → projection done = projection execution only.
        VotingMetrics.VoteWorkerExecutionDurationSeconds.Record(
            Math.Max(0, (completedAtUtc - stage2WorkerStartedAtUtc).TotalSeconds), tags);

        VotingMetrics.VotesProcessed.Add(1, tags);

        await context.Publish(new PollResultsUpdatedEvent(results), context.CancellationToken);
    }
}
