using System.Diagnostics;
using AsynchronousVoting.Api.Hubs;
using AsynchronousVoting.Api.Monitoring;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Voting.Application.DTOs;

namespace AsynchronousVoting.Api.Messaging.Consumers;

public class PollResultsUpdatedEventConsumer : IConsumer<PollResultsUpdatedEvent>
{
    private readonly IHubContext<ResultsHub> _hubContext;

    public PollResultsUpdatedEventConsumer(IHubContext<ResultsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task Consume(ConsumeContext<PollResultsUpdatedEvent> context)
    {
        var consumerStartedAtUtc = DateTime.UtcNow;
        var pollResults = context.Message.PollResults;
        var tags = new TagList
        {
            { "architecture", "async" }
        };

        var signalRStartedAtUtc = DateTime.UtcNow;
        await _hubContext
            .Clients.Group(pollResults.PollId.ToString())
            .SendAsync("PollResultsUpdated", pollResults, context.CancellationToken);
        var completedAtUtc = DateTime.UtcNow;

        VotingMetrics.PollResultsUpdatedEventQueueDelaySeconds.Record(
            Math.Max(0, (consumerStartedAtUtc - context.Message.PublishedAtUtc).TotalSeconds),
            tags);
        VotingMetrics.SignalRSendDurationSeconds.Record(
            Math.Max(0, (completedAtUtc - signalRStartedAtUtc).TotalSeconds),
            tags);
        VotingMetrics.ResultsNotificationCompletionDurationSeconds.Record(
            Math.Max(0, (completedAtUtc - context.Message.RequestStartedAtUtc).TotalSeconds),
            tags);
    }
}
