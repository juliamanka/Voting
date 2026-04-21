using Voting.Application.DTOs;
using Voting.Domain.Entities;

namespace Voting.Application.Interfaces;

public interface IVoteWriteService
{
    Task<VoteRecord> WriteVoteAsync(VoteRequest voteRequest, CancellationToken cancellationToken);
}
