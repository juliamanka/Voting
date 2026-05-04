using Voting.Application.DTOs;

namespace Voting.Application.Interfaces;

public interface IPollResultsReader
{
    Task<List<PollResults>> GetAllAsync(CancellationToken cancellationToken);

    Task<PollResults?> GetByPollIdAsync(Guid pollId, CancellationToken cancellationToken);
}
