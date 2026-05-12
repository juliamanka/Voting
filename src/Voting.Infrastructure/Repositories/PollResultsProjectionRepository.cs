using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

    public async Task<List<PollResultsProjection>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _context.PollResultsProjections
            .AsNoTracking()
            .Include(p => p.Options)
            .OrderBy(p => p.PollTitle)
            .ToListAsync(cancellationToken);
    }

    public async Task<PollResultsProjection?> GetByPollIdAsync(Guid pollId, CancellationToken cancellationToken)
    {
        return await _context.PollResultsProjections
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
            var isFirstProcessing = await TryAddAuditLogAsync(auditLog, ownsTransaction, transaction, cancellationToken);

            if (!isFirstProcessing)
            {
                return await GetRequiredByPollIdAsync(poll.PollId, cancellationToken);
            }

            var pollProjectionExists = await IncrementPollProjectionAsync(poll, vote, cancellationToken);

            if (!pollProjectionExists)
            {
                await CreatePollProjectionWithVoteAsync(poll, vote, cancellationToken);
            }
            else
            {
                var optionProjectionExists = await IncrementOptionProjectionAsync(vote, cancellationToken);

                if (!optionProjectionExists)
                {
                    await CreateOptionProjectionWithVoteAsync(poll, vote, cancellationToken);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            if (ownsTransaction)
            {
                await transaction!.CommitAsync(cancellationToken);
            }

            return await GetRequiredByPollIdAsync(poll.PollId, cancellationToken);
        }
        catch
        {
            if (ownsTransaction)
            {
                await transaction!.RollbackAsync(cancellationToken);
            }

            throw;
        }
    }
    
    private async Task<bool> TryAddAuditLogAsync(
        VoteAuditLog auditLog,
        bool ownsTransaction,
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await _context.VoteAuditLogs.AddAsync(auditLog, cancellationToken);

            // Force unique VoteId check before incrementing projections.
            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            _context.Entry(auditLog).State = EntityState.Detached;

            if (ownsTransaction)
            {
                await transaction!.RollbackAsync(cancellationToken);
            }

            return false;
        }
    }
    private async Task<bool> IncrementPollProjectionAsync(
        Poll poll,
        VoteRecord vote,
        CancellationToken cancellationToken)
    {
        var rows = await _context.PollResultsProjections
            .Where(p => p.PollId == poll.PollId)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.PollTitle, poll.Question)
                    .SetProperty(p => p.TotalVotes, p => p.TotalVotes + 1)
                    .SetProperty(p => p.LastUpdatedAtUtc, vote.Timestamp),
                cancellationToken);

        return rows > 0;
    }
    private async Task CreatePollProjectionWithVoteAsync(
        Poll poll,
        VoteRecord vote,
        CancellationToken cancellationToken)
    {
        await _context.PollResultsProjections.AddAsync(new PollResultsProjection
        {
            PollId = poll.PollId,
            PollTitle = poll.Question,
            TotalVotes = 1,
            LastUpdatedAtUtc = vote.Timestamp,
            Options = poll.Options
                .OrderBy(o => o.OrderIndex)
                .Select(o => new PollOptionResultsProjection
                {
                    PollId = poll.PollId,
                    PollOptionId = o.PollOptionId,
                    OptionText = o.Text,
                    OrderIndex = o.OrderIndex,
                    VoteCount = o.PollOptionId == vote.PollOptionId ? 1 : 0
                })
                .ToList()
        }, cancellationToken);
    }
    private async Task<bool> IncrementOptionProjectionAsync(
        VoteRecord vote,
        CancellationToken cancellationToken)
    {
        var rows = await _context.PollOptionResultsProjections
            .Where(p => p.PollId == vote.PollId && p.PollOptionId == vote.PollOptionId)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.VoteCount, p => p.VoteCount + 1),
                cancellationToken);

        return rows > 0;
    }

    private async Task CreateOptionProjectionWithVoteAsync(
        Poll poll,
        VoteRecord vote,
        CancellationToken cancellationToken)
    {
        var option = poll.Options.First(o => o.PollOptionId == vote.PollOptionId);

        await _context.PollOptionResultsProjections.AddAsync(new PollOptionResultsProjection
        {
            PollId = poll.PollId,
            PollOptionId = option.PollOptionId,
            OptionText = option.Text,
            OrderIndex = option.OrderIndex,
            VoteCount = 1
        }, cancellationToken);
    }
    
    private async Task<PollResultsProjection> GetRequiredByPollIdAsync(
        Guid pollId,
        CancellationToken cancellationToken)
    {
        return await GetByPollIdAsync(pollId, cancellationToken)
               ?? throw new InvalidOperationException(
                   $"Poll results projection {pollId} was not found.");
    }
}
