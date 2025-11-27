using Voting.Application.DTOs;

namespace Voting.Application.Interfaces;

public interface IPollService
{
    Task<IEnumerable<PollDto>> GetAvailablePollsAsync(CancellationToken cancellationToken);
    
    Task<PollDto> GetPollWithOptions(Guid pollId, CancellationToken cancellationToken);
    
    Task<List<PollResults>> GetAllVotesForPolls(CancellationToken cancellationToken);
    
    Task<PollResults?> GetVotesForPoll(Guid pollId, CancellationToken cancellationToken);

}