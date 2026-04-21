using Microsoft.EntityFrameworkCore;
using Voting.Domain.Entities;
using Voting.Domain.Repository;
using Voting.Infrastructure.Database;

namespace Voting.Infrastructure.Repositories;

public class PollResultsProjectionRepository : IPollResultsProjectionRepository
{
    private readonly VotingDbContext _context;

    public PollResultsProjectionRepository(VotingDbContext context)
    {
        _context = context;
    }

    public Task<List<PollResultsProjection>> GetAllAsync(CancellationToken cancellationToken)
    {
        return _context.PollResultsProjections
            .AsNoTracking()
            .Include(p => p.Options)
            .OrderBy(p => p.PollTitle)
            .ToListAsync(cancellationToken);
    }

    public Task<PollResultsProjection?> GetByPollIdAsync(Guid pollId, CancellationToken cancellationToken)
    {
        return _context.PollResultsProjections
            .AsNoTracking()
            .Include(p => p.Options)
            .FirstOrDefaultAsync(p => p.PollId == pollId, cancellationToken);
    }

    public async Task<PollResultsProjection> GetOrCreateAsync(Poll poll, CancellationToken cancellationToken)
    {
        var projection = await _context.PollResultsProjections
            .Include(p => p.Options)
            .FirstOrDefaultAsync(p => p.PollId == poll.PollId, cancellationToken);

        if (projection is not null)
        {
            return projection;
        }

        projection = new PollResultsProjection
        {
            PollId = poll.PollId,
            PollTitle = poll.Question,
            TotalVotes = 0,
            LastUpdatedAtUtc = DateTime.UtcNow,
            Options = poll.Options
                .OrderBy(o => o.OrderIndex)
                .Select(o => new PollOptionResultsProjection
                {
                    PollId = poll.PollId,
                    PollOptionId = o.PollOptionId,
                    OrderIndex = o.OrderIndex,
                    OptionText = o.Text,
                    VoteCount = 0
                })
                .ToList()
        };

        await _context.PollResultsProjections.AddAsync(projection, cancellationToken);
        return projection;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
