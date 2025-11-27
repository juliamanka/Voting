using Asynchronous.Api.Requests;
using MassTransit;
using Voting.Application.Interfaces;

namespace AsynchronousVoting.Api;

public class AsyncVoteNotifier : IVoteNotifier
{
    private readonly IPublishEndpoint _publishEndpoint;

    public AsyncVoteNotifier(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task NotifyVoteAsync(Guid pollId, Guid optionId, string? userId, CancellationToken cancellationToken = default)
    {
        var cmd = new CastVoteRequest()
        {
            PollId = pollId,
            PollOptionId = optionId,
            UserId = userId
        };

        return _publishEndpoint.Publish(cmd, cancellationToken);
    }
}