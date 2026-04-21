using AsynchronousVoting.Api.Hubs;
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
        var pollResults = context.Message.PollResults;

        await _hubContext
            .Clients.Group(pollResults.PollId.ToString())
            .SendAsync("PollResultsUpdated", pollResults, context.CancellationToken);
    }
}
