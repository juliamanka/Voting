using MassTransit;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;

namespace HybridVoting.Api;

public class HybridVoteNotifier : IVoteNotifier
{
    private readonly IPublishEndpoint _publishEndpoint;

    public HybridVoteNotifier(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task NotifyVoteAsync(Guid pollId, Guid optionId, string? userId, CancellationToken cancellationToken = default)
    {
      
        var evt = new VoteRecordedEvent(
            VoteId: Guid.NewGuid(),      // nie jest używany dalej
            PollId: pollId,
            PollOptionId: optionId,
            UserId: null,
            TimeStamp: DateTime.UtcNow
        );

        return _publishEndpoint.Publish<VoteRecordedEvent>(evt, cancellationToken);
    }
}