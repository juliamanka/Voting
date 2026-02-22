using AutoMapper;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;
using Voting.Domain.Repository;

namespace Voting.Application.Services;

public class PollService : IPollService
{
    private readonly IPollRepository _pollRepository;
    private readonly IVoteRepository _voteRepository;
    private readonly IMapper _mapper;

    public PollService(IPollRepository pollRepository, IMapper mapper, IVoteRepository voteRepository)
    {
        _pollRepository = pollRepository;
        _mapper = mapper;
        _voteRepository = voteRepository;
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
        var polls = await _pollRepository.GetAllAsync(cancellationToken);
        
        var votes = await _voteRepository.GetVotes(cancellationToken);
        var votesInPoll = votes.GroupBy(v => new { v.PollId, v.PollOptionId })
            .Select(g => new
            {
                pollId = g.Key.PollId,
                optionId = g.Key.PollOptionId,
                count = g.Count()
            })
            .ToList();

        var response = polls.Select(p => new PollResults()
        {
            PollId = p.PollId,
            PollTitle = p.Question,
            Options = Enumerable.Select(p.Options, o => new Options()
            {
                OptionId = o.PollOptionId,
                OptionText = o.Text,
                VoteCount = votesInPoll
                    .Where(v => v.pollId == p.PollId && v.optionId == o.PollOptionId)
                    .Select(v => v.count)
                    .FirstOrDefault()
            }).ToList()
        }).ToList();

        return response;
    }
    
    public async Task<PollResults?> GetVotesForPoll(Guid pollId, CancellationToken cancellationToken)
    {
        var poll = await _pollRepository.GetByIdAsync(pollId, cancellationToken);
        if (poll == null) return null;

        var voteCounts = await _voteRepository.GetVoteCountsByPollIdAsync(pollId, cancellationToken);

        return new PollResults
        {
            PollId = poll.PollId,
            PollTitle = poll.Question,
            Options = poll.Options.Select(o => new Options
            {
                OptionId = o.PollOptionId,
                OptionText = o.Text,
                VoteCount = voteCounts.TryGetValue(o.PollOptionId, out var count) ? count : 0
            }).ToList()
        };
    }
}