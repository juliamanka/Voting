using Voting.Domain.Entities;
using Voting.Domain.Repository;
using Voting.Infrastructure.Database;

namespace Voting.Infrastructure.Repositories;

public class VoteAuditLogRepository : IVoteAuditLogRepository
{
    private readonly VotingDbContext _context;

    public VoteAuditLogRepository(VotingDbContext context)
    {
        _context = context;
    }

    public Task AddAsync(VoteAuditLog auditLog, CancellationToken cancellationToken)
    {
        return _context.VoteAuditLogs.AddAsync(auditLog, cancellationToken).AsTask();
    }
}
