namespace Voting.Application.Interfaces;

public interface IVoteNotifier
{
    Task<Guid> NotifyVoteAsync(Guid pollId, Guid optionId, string? userId, CancellationToken cancellationToken = default);
}
