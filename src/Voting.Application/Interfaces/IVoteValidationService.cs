using Voting.Application.DTOs;

namespace Voting.Application.Interfaces;

public interface IVoteValidationService
{
    Task ValidateAsync(VoteRequest voteRequest, CancellationToken cancellationToken);
}
