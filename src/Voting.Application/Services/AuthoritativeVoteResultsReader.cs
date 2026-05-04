using Voting.Application.DTOs;
using Voting.Application.Interfaces;
using Voting.Domain.Entities;
using Voting.Domain.Repository;

namespace Voting.Application.Services;

public class AuthoritativeVoteResultsReader : IPollResultsReader
{
    private readonly IPollRepository _pollRepository;
    private readonly IVoteRepository _voteRepository;

    public AuthoritativeVoteResultsReader(
        IPollRepository pollRepository,
        IVoteRepository voteRepository)
    {
        _pollRepository = pollRepository;
        _voteRepository = voteRepository;
    }

    public async Task<List<PollResults>> GetAllAsync(CancellationToken cancellationToken)
    {
        var polls = (await _pollRepository.GetAllAsync(cancellationToken))
            .OrderBy(p => p.Question)
            .ToList();

        var results = new List<PollResults>(polls.Count);
        foreach (var poll in polls)
        {
            results.Add(await BuildPollResultsAsync(poll, cancellationToken));
        }

        return results;
    }

    public async Task<PollResults?> GetByPollIdAsync(Guid pollId, CancellationToken cancellationToken)
    {
        var poll = await _pollRepository.GetByIdAsync(pollId, cancellationToken);
        return poll is null ? null : await BuildPollResultsAsync(poll, cancellationToken);
    }

    private async Task<PollResults> BuildPollResultsAsync(Poll poll, CancellationToken cancellationToken)
    {
        var voteCounts = await _voteRepository.GetVoteCountsByPollIdAsync(poll.PollId, cancellationToken);
        var latestVoteTimestamp = await _voteRepository.GetLatestVoteTimestampAsync(poll.PollId, cancellationToken)
            ?? poll.CreatedAt;

        return new PollResults
        {
            PollId = poll.PollId,
            PollTitle = poll.Question,
            TotalVotes = voteCounts.Values.Sum(),
            LastUpdatedAtUtc = latestVoteTimestamp,
            Options = poll.Options
                .OrderBy(o => o.OrderIndex)
                .Select(o => new Options
                {
                    OptionId = o.PollOptionId,
                    OptionText = o.Text,
                    VoteCount = voteCounts.GetValueOrDefault(o.PollOptionId)
                })
                .ToList()
        };
    }
}
