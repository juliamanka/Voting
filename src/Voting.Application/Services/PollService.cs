using AutoMapper;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;
using Voting.Domain.Repository;

namespace Voting.Application.Services;

public class PollService : IPollService
{
    private readonly IPollRepository _pollRepository;
    private readonly IPollResultsProjectionRepository _projectionRepository;
    private readonly IMapper _mapper;

    public PollService(
        IPollRepository pollRepository,
        IMapper mapper,
        IPollResultsProjectionRepository projectionRepository)
    {
        _pollRepository = pollRepository;
        _mapper = mapper;
        _projectionRepository = projectionRepository;
    }

    public async Task<IEnumerable<PollDto>> GetAvailablePollsAsync(CancellationToken cancellationToken)
    {   
        var polls = await _pollRepository.GetActivePollsWithOptionsAsync(cancellationToken);
        return _mapper.Map<IEnumerable<PollDto>>(polls);
    }

    public async Task<PollDto> GetPollWithOptions(Guid pollId, CancellationToken cancellationToken)
    {
        var poll = await _pollRepository.GetByIdAsync(pollId, cancellationToken);
        return _mapper.Map<PollDto>(poll);
    }

    public async Task<List<PollResults>> GetAllVotesForPolls(CancellationToken cancellationToken)
    {
        var projections = await _projectionRepository.GetAllAsync(cancellationToken);

        return projections.Select(p => new PollResults
        {
            PollId = p.PollId,
            PollTitle = p.PollTitle,
            TotalVotes = p.TotalVotes,
            LastUpdatedAtUtc = p.LastUpdatedAtUtc,
            Options = p.Options
                .OrderBy(o => o.OrderIndex)
                .Select(o => new Options
            {
                    OptionId = o.PollOptionId,
                    OptionText = o.OptionText,
                    VoteCount = o.VoteCount
                })
                .ToList()
        }).ToList();
    }
    
    public async Task<PollResults?> GetVotesForPoll(Guid pollId, CancellationToken cancellationToken)
    {
        var projection = await _projectionRepository.GetByPollIdAsync(pollId, cancellationToken);
        if (projection is null)
        {
            return null;
        }

        return new PollResults
        {
            PollId = projection.PollId,
            PollTitle = projection.PollTitle,
            TotalVotes = projection.TotalVotes,
            LastUpdatedAtUtc = projection.LastUpdatedAtUtc,
            Options = projection.Options
                .OrderBy(o => o.OrderIndex)
                .Select(o => new Options
                {
                    OptionId = o.PollOptionId,
                    OptionText = o.OptionText,
                    VoteCount = o.VoteCount
                })
                .ToList()
        };
    }
}
