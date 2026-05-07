using Voting.Application.DTOs;

namespace Voting.Application.Interfaces;

public interface IVotingService
{
    Task<VoteProcessingResult> ProcessVoteAsync(VoteRequest voteRequest, CancellationToken cancellationToken);
}
