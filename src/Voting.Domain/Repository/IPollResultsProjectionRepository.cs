using Voting.Domain.Entities;

namespace Voting.Domain.Repository;

public interface IPollResultsProjectionRepository
{
    Task<List<PollResultsProjection>> GetAllAsync(CancellationToken cancellationToken);

    Task<PollResultsProjection?> GetByPollIdAsync(Guid pollId, CancellationToken cancellationToken);

    Task<PollResultsProjection> GetOrCreateAsync(Poll poll, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
