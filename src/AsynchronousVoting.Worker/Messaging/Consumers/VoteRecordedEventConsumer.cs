using System.Diagnostics;
using System.Diagnostics.Metrics;
using AsynchronousVoting.Worker.Monitoring;
using MassTransit;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;
using Voting.Domain.Entities;
using Voting.Domain.Enums;

namespace AsynchronousVoting.Worker.Messaging.Consumers;

public class VoteRecordedEventConsumer : IConsumer<VoteRecordedEvent>
{
    private readonly IVoteProjectionAndAuditService _voteProjectionAndAuditService;
    private readonly ILogger<VoteRecordedEventConsumer> _logger;
    private readonly string _instanceId;

    public VoteRecordedEventConsumer(
        IVoteProjectionAndAuditService voteProjectionAndAuditService,
        ILogger<VoteRecordedEventConsumer> logger,
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

        VotingMetrics.VoteProjectionCompletionDurationSeconds.Record(
            Math.Max(0, (completedAtUtc - msg.RequestStartedAtUtc).TotalSeconds), tags);

        VotingMetrics.VoteRecordedEventQueueDelaySeconds.Record(
            Math.Max(0, (stage2WorkerStartedAtUtc - msg.PublishedAtUtc).TotalSeconds), tags);

        VotingMetrics.VotesProcessed.Add(1, tags);

        await context.Publish(
            new PollResultsUpdatedEvent(results, msg.RequestStartedAtUtc, DateTime.UtcNow),
            context.CancellationToken);
    }
}
