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
}
