using Microsoft.EntityFrameworkCore;
using Voting.Domain.Entities;
using Voting.Domain.Repository;
using Voting.Infrastructure.Database;

namespace Voting.Infrastructure.Repositories;

public class PollRepository  : IPollRepository
{
    private readonly VotingDbContext _context;

    public PollRepository(VotingDbContext context)
    {
        _context = context;
    }

    public async Task<Poll?> GetByIdAsync(Guid pollId, CancellationToken cancellationToken)
    {
        return await _context.Polls.Include(p => p.Options)
            .FirstOrDefaultAsync(p => p.PollId == pollId, cancellationToken);
    }
    
    public async Task<IEnumerable<Poll> > GetAllAsync(CancellationToken cancellationToken)
    {
        return await _context.Polls
            .Include(p=>p.Options)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
    
    public async Task<IEnumerable<Poll>> GetActivePollsWithOptionsAsync(CancellationToken cancellationToken)
    {
        return await _context.Polls
            .AsNoTracking() 
            .Include(p => p.Options) 
            .Where(p => p.IsActive)
            .ToListAsync(cancellationToken);
    }
}