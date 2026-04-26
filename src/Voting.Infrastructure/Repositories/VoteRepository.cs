using Microsoft.EntityFrameworkCore;
using Voting.Application.Exceptions;
using Voting.Domain.Entities;
using Voting.Domain.Enums;
using Voting.Domain.Repository;
using Voting.Infrastructure.Database;

namespace Voting.Infrastructure.Repositories;

public class VoteRepository : IVoteRepository
{
    private readonly VotingDbContext _context;

    public VoteRepository(VotingDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<VoteRecord>> GetVotes(CancellationToken cancellationToken)
    {
        var votes = await _context.Votes
            .ToListAsync(cancellationToken: cancellationToken);

        return votes;
    }

    public async Task<VoteRecord> AddVoteAsync(VoteRecord vote, CancellationToken cancellationToken)
    {
        try
        {
            await _context.Votes.AddAsync(vote, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            _context.Entry(vote).State = EntityState.Detached;
            throw new DuplicateVoteException(vote.PollId, vote.UserId);
        }

        return vote;
    }

    public Task<bool> HasUserVotedAsync(Guid pollId, string userId, CancellationToken cancellationToken)
    {
        return _context.Votes
            .AnyAsync(v => v.PollId == pollId && v.UserId == userId && v.Status == VoteStatus.Counted, cancellationToken);
    }
    
    public async Task<Dictionary<Guid, int>> GetVoteCountsByPollIdAsync(Guid pollId, CancellationToken cancellationToken)
    {
        // Wykonuje grupowanie po stronie SQL: SELECT PollOptionId, COUNT(*) ... GROUP BY PollOptionId
        return await _context.Votes
            .Where(v => v.PollId == pollId && v.Status == VoteStatus.Counted)
            .GroupBy(v => v.PollOptionId)
            .Select(g => new { OptionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OptionId, x => x.Count, cancellationToken);
    }

    public async Task<DateTime?> GetLatestVoteTimestampAsync(Guid pollId, CancellationToken cancellationToken)
    {
        return await _context.Votes
            .Where(v => v.PollId == pollId && v.Status == VoteStatus.Counted)
            .MaxAsync(v => (DateTime?)v.Timestamp, cancellationToken);
    }
}
