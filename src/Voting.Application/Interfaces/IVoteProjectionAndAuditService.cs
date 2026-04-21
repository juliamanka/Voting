using Voting.Application.DTOs;
using Voting.Domain.Entities;

namespace Voting.Application.Interfaces;

public interface IVoteProjectionAndAuditService
{
    Task<PollResults> ApplyVoteAcceptedAsync(
        VoteRecord vote,
        string architecture,
        CancellationToken cancellationToken);
}
