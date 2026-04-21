using Microsoft.EntityFrameworkCore;
using Voting.Domain.Entities;
using Voting.Domain.Repository;
using Voting.Infrastructure.Database;

namespace Voting.Infrastructure.Repositories;

public class VoterEligibilityRepository : IVoterEligibilityRepository
{
    private readonly VotingDbContext _context;

    public VoterEligibilityRepository(VotingDbContext context)
    {
        _context = context;
    }

    public async Task<VoterEligibility> GetOrCreateAsync(string userId, CancellationToken cancellationToken)
    {
        var eligibility = await _context.VoterEligibilities
            .FirstOrDefaultAsync(v => v.UserId == userId, cancellationToken);

        if (eligibility is not null)
        {
            return eligibility;
        }

        eligibility = new VoterEligibility
        {
            UserId = userId,
            IsEligible = true,
            EligibilitySource = "Registry",
            ChecksPerformed = 0,
            CreatedAtUtc = DateTime.UtcNow,
            LastCheckedAtUtc = DateTime.UtcNow
        };

        await _context.VoterEligibilities.AddAsync(eligibility, cancellationToken);
        return eligibility;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
