using Microsoft.EntityFrameworkCore;
using Voting.Domain.Entities;
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
        await _context.Votes.AddAsync(vote, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return vote;
    }
}