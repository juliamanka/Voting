using Voting.Domain.Entities;

namespace Voting.Domain.Repository;

public interface IVoteRepository
{
    Task<VoteRecord> AddVoteAsync(VoteRecord vote, CancellationToken cancellationToken);

    Task<bool> HasUserVotedAsync(Guid pollId, string userId, CancellationToken cancellationToken);
}
