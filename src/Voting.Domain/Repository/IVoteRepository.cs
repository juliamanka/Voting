using Voting.Domain.Entities;

namespace Voting.Domain.Repository;

public interface IVoteRepository
{
    Task<IEnumerable<VoteRecord>> GetVotes(CancellationToken cancellationToken);
    
    Task<VoteRecord> AddVoteAsync(VoteRecord vote, CancellationToken cancellationToken);

    Task<bool> HasUserVotedAsync(Guid pollId, string userId, CancellationToken cancellationToken);
    
    Task<Dictionary<Guid, int>> GetVoteCountsByPollIdAsync(Guid pollId, CancellationToken cancellationToken);

    Task<DateTime?> GetLatestVoteTimestampAsync(Guid pollId, CancellationToken cancellationToken);
}
