using Voting.Domain.Entities;

namespace Voting.Domain.Repository;

public interface IVoteAuditLogRepository
{
    Task AddAsync(VoteAuditLog auditLog, CancellationToken cancellationToken);
}
