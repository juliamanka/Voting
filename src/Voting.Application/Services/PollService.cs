using Voting.Application.DTOs;
using Voting.Application.Exceptions;
using Voting.Application.Interfaces;
using Voting.Domain.Repository;

namespace Voting.Application.Services;

public class PollService : IPollService
{
    private readonly IPollRepository _pollRepository;
    private readonly IPollResultsReader _pollResultsReader;

    public PollService(
        IPollRepository pollRepository,
        IPollResultsReader pollResultsReader)
    {
        _pollRepository = pollRepository;
        _pollResultsReader = pollResultsReader;
    }

    public async Task<IEnumerable<PollDto>> GetAvailablePollsAsync(CancellationToken cancellationToken)
    {   
        var polls = await _pollRepository.GetActivePollsWithOptionsAsync(cancellationToken);
        var pollDtos = polls.Select(p => new PollDto
        {
            IsActive = p.IsActive,
            Options = p.Options.OrderBy(o => o.OrderIndex).Select(o => new PollOptionDto()
            {
                PollOptionId = o.PollOptionId,
                Text = o.Text,
                OrderIndex = o.OrderIndex
            }).ToList(),
            PollId = p.PollId,
            Question = p.Question,
            RequiresEligibilityCheck = p.RequiresEligibilityCheck
        }).ToList();

        return pollDtos;
    }

    public async Task<PollDto> GetPollWithOptions(Guid pollId, CancellationToken cancellationToken)
    {
        var poll = await _pollRepository.GetByIdAsync(pollId, cancellationToken)
            ?? throw new NotFoundException("Poll", pollId);

        return new PollDto()
        {
            PollId = poll.PollId,
            IsActive = poll.IsActive,
            Options = poll.Options.OrderBy(o => o.OrderIndex).Select(o => new PollOptionDto()
            {
                PollOptionId = o.PollOptionId,
                Text = o.Text,
                OrderIndex = o.OrderIndex
            }).ToList(),
            Question = poll.Question,
            RequiresEligibilityCheck = poll.RequiresEligibilityCheck
        };
    }

    public async Task<List<PollResults>> GetAllPollResults(CancellationToken cancellationToken)
    {
        return await _pollResultsReader.GetAllAsync(cancellationToken);
    }
    
    public async Task<PollResults?> GetPollResults(Guid pollId, CancellationToken cancellationToken)
    {
        return await _pollResultsReader.GetByPollIdAsync(pollId, cancellationToken);
    }
}
