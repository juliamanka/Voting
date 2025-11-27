using Voting.Application.DTOs;

namespace Voting.Application.Interfaces;

public interface IVotingService
{
    Task<VoteResponse> ProcessVoteAsync(VoteRequest voteRequest, CancellationToken cancellationToken);
}