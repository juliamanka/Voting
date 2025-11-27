using Voting.Domain.Entities;

namespace Voting.Domain.Repository;

public interface IPollRepository
{
    Task<Poll?> GetByIdAsync(Guid pollId, CancellationToken cancellationToken);

    Task<IEnumerable<Poll>> GetAllAsync(CancellationToken cancellationToken);

    Task<IEnumerable<Poll>> GetActivePollsWithOptionsAsync(CancellationToken cancellationToken);
}