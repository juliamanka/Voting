using Voting.Application.DTOs;
using Voting.Application.Interfaces;
using Voting.Domain.Repository;

namespace Voting.Application.Services;

public class ProjectionPollResultsReader : IPollResultsReader
{
    private readonly IPollResultsProjectionRepository _projectionRepository;

    public ProjectionPollResultsReader(IPollResultsProjectionRepository projectionRepository)
    {
        _projectionRepository = projectionRepository;
    }

    public async Task<List<PollResults>> GetAllAsync(CancellationToken cancellationToken)
    {
        var projections = await _projectionRepository.GetAllAsync(cancellationToken);
        return projections.Select(Map).ToList();
    }

    public async Task<PollResults?> GetByPollIdAsync(Guid pollId, CancellationToken cancellationToken)
    {
        var projection = await _projectionRepository.GetByPollIdAsync(pollId, cancellationToken);
        return projection is null ? null : Map(projection);
    }

    private static PollResults Map(Voting.Domain.Entities.PollResultsProjection projection)
    {
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
