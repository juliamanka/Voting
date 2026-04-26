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

    public async Task<PollResultsProjection> ApplyVoteAcceptedAsync(
        Poll poll,
        VoteRecord vote,
        VoteAuditLog auditLog,
        CancellationToken cancellationToken)
    {
        var ownsTransaction = _context.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            await _context.VoteAuditLogs.AddAsync(auditLog, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var projectionRows = await _context.PollResultsProjections
                .Where(p => p.PollId == poll.PollId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.PollTitle, poll.Question)
                    .SetProperty(p => p.TotalVotes, p => p.TotalVotes + 1)
                    .SetProperty(p => p.LastUpdatedAtUtc, vote.Timestamp),
                    cancellationToken);

            if (projectionRows == 0)
            {
                await CreateProjectionAsync(poll, cancellationToken);
                await _context.PollResultsProjections
                    .Where(p => p.PollId == poll.PollId)
                    .ExecuteUpdateAsync(setters => setters
                            .SetProperty(p => p.TotalVotes, p => p.TotalVotes + 1)
                            .SetProperty(p => p.LastUpdatedAtUtc, vote.Timestamp),
                        cancellationToken);
            }

            var optionRows = await _context.PollOptionResultsProjections
                .Where(p => p.PollId == poll.PollId && p.PollOptionId == vote.PollOptionId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.VoteCount, p => p.VoteCount + 1),
                    cancellationToken);

            if (optionRows == 0)
            {
                await CreateOptionProjectionAsync(poll, vote.PollOptionId, cancellationToken);
                await _context.PollOptionResultsProjections
                    .Where(p => p.PollId == poll.PollId && p.PollOptionId == vote.PollOptionId)
                    .ExecuteUpdateAsync(setters => setters
                            .SetProperty(p => p.VoteCount, p => p.VoteCount + 1),
                        cancellationToken);
            }

            if (ownsTransaction)
            {
                await transaction!.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            _context.Entry(auditLog).State = EntityState.Detached;
            if (ownsTransaction)
            {
                await transaction!.RollbackAsync(cancellationToken);
            }
        }

        return await GetProjectionAsync(poll.PollId, cancellationToken);
    }

    private Task<PollResultsProjection> GetProjectionAsync(Guid pollId, CancellationToken cancellationToken)
    {
        return _context.PollResultsProjections
            .AsNoTracking()
            .Include(p => p.Options)
            .FirstAsync(p => p.PollId == pollId, cancellationToken);
    }

    private async Task CreateProjectionAsync(Poll poll, CancellationToken cancellationToken)
    {
        var projection = new PollResultsProjection
        {
            PollId = poll.PollId,
            PollTitle = poll.Question,
            TotalVotes = 0,
            LastUpdatedAtUtc = poll.CreatedAt,
            Options = poll.Options
                .OrderBy(o => o.OrderIndex)
                .Select(o => new PollOptionResultsProjection
                {
                    PollId = poll.PollId,
                    PollOptionId = o.PollOptionId,
                    OptionText = o.Text,
                    OrderIndex = o.OrderIndex,
                    VoteCount = 0
                })
                .ToList()
        };

        await _context.PollResultsProjections.AddAsync(projection, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task CreateOptionProjectionAsync(Poll poll, Guid pollOptionId, CancellationToken cancellationToken)
    {
        var option = poll.Options.First(o => o.PollOptionId == pollOptionId);
        await _context.PollOptionResultsProjections.AddAsync(new PollOptionResultsProjection
        {
            PollId = poll.PollId,
            PollOptionId = option.PollOptionId,
            OptionText = option.Text,
            OrderIndex = option.OrderIndex,
            VoteCount = 0
        }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
