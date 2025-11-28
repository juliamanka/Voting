using HybridVoting.Api.Hubs;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;

namespace HybridVoting.Api.Messaging.Consumers;

public class VoteRecordedEventConsumer : IConsumer<VoteRecordedEvent>
{
    private readonly IPollService _voteResultsService;
    private readonly IHubContext<ResultsHub> _hubContext;

    public VoteRecordedEventConsumer(
        IPollService voteResultsService,
        IHubContext<ResultsHub> hubContext)
    {
        _voteResultsService = voteResultsService;
        _hubContext = hubContext;
    }

    public async Task Consume(ConsumeContext<VoteRecordedEvent> context)
    {
        Console.WriteLine($"[SignalR] VoteRecordedEvent received for poll {context.Message.PollId}, option {context.Message.PollOptionId}");
        
        var pollId = context.Message.PollId;

        var pollResults =
            await _voteResultsService.GetVotesForPoll(pollId, context.CancellationToken);

        if (pollResults is null)
            return;

        await _hubContext
            .Clients.Group(pollId.ToString())
            .SendAsync("PollResultsUpdated", pollResults, context.CancellationToken);
    }
}