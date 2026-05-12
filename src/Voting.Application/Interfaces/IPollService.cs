using Voting.Application.DTOs;

namespace Voting.Application.Interfaces;

public interface IPollService
{
    Task<IEnumerable<PollDto>> GetAvailablePollsAsync(CancellationToken cancellationToken);
    
    Task<PollDto> GetPollWithOptions(Guid pollId, CancellationToken cancellationToken);
    
    Task<List<PollResults>> GetAllPollResults(CancellationToken cancellationToken);
    
    Task<PollResults?> GetPollResults(Guid pollId, CancellationToken cancellationToken);
}
