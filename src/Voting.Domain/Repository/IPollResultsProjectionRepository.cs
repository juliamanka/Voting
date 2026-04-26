using Voting.Domain.Entities;

namespace Voting.Domain.Repository;

public interface IPollResultsProjectionRepository
{
    Task<List<PollResultsProjection>> GetAllAsync(CancellationToken cancellationToken);

    Task<PollResultsProjection?> GetByPollIdAsync(Guid pollId, CancellationToken cancellationToken);
    
    Task<PollResultsProjection> ApplyVoteAcceptedAsync(
        Poll poll,
        VoteRecord vote,
        VoteAuditLog auditLog,
        CancellationToken cancellationToken);
}
