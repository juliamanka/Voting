using Voting.Domain.Entities;

namespace Voting.Domain.Repository;

public interface IVoterEligibilityRepository
{
    Task<VoterEligibility> GetOrCreateAsync(string userId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
