using AutoMapper;
using Voting.Application.DTOs;
using Voting.Application.Exceptions;
using Voting.Application.Interfaces;
using Voting.Domain.Repository;

namespace Voting.Application.Services;

public class PollService : IPollService
{
    private readonly IPollRepository _pollRepository;
    private readonly IPollResultsReader _pollResultsReader;
    private readonly IMapper _mapper;

    public PollService(
        IPollRepository pollRepository,
        IMapper mapper,
        IPollResultsReader pollResultsReader)
    {
        _pollRepository = pollRepository;
        _mapper = mapper;
        _pollResultsReader = pollResultsReader;
    }

    public async Task<IEnumerable<PollDto>> GetAvailablePollsAsync(CancellationToken cancellationToken)
    {   
        var polls = await _pollRepository.GetActivePollsWithOptionsAsync(cancellationToken);
        return _mapper.Map<IEnumerable<PollDto>>(polls);
    }

    public async Task<PollDto> GetPollWithOptions(Guid pollId, CancellationToken cancellationToken)
    {
        var poll = await _pollRepository.GetByIdAsync(pollId, cancellationToken)
            ?? throw new NotFoundException("Poll", pollId);

        return _mapper.Map<PollDto>(poll);
    }

    public async Task<List<PollResults>> GetAllVotesForPolls(CancellationToken cancellationToken)
    {
        return await _pollResultsReader.GetAllAsync(cancellationToken);
    }
    
    public async Task<PollResults?> GetVotesForPoll(Guid pollId, CancellationToken cancellationToken)
    {
        return await _pollResultsReader.GetByPollIdAsync(pollId, cancellationToken);
    }
}
