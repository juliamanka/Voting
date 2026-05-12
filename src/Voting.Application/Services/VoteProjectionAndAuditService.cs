using Microsoft.Extensions.Options;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;
using Voting.Application.Options;
using Voting.Domain.Entities;
using Voting.Domain.Repository;

namespace Voting.Application.Services;

public class VoteProjectionAndAuditService : IVoteProjectionAndAuditService
{
    private readonly IPollRepository _pollRepository;
    private readonly IPollResultsProjectionRepository _projectionRepository;
    private readonly ProjectionOptions _projectionOptions;

    public VoteProjectionAndAuditService(
        IPollRepository pollRepository,
        IPollResultsProjectionRepository projectionRepository,
        IOptions<ProjectionOptions> projectionOptions)
    {
        _pollRepository = pollRepository;
        _projectionRepository = projectionRepository;
        _projectionOptions = projectionOptions.Value;
    }

    public async Task<PollResults> ApplyVoteAcceptedAsync(
        VoteRecord vote,
        string architecture,
        CancellationToken cancellationToken)
    {
        if (_projectionOptions.DelayMs > 0)
        {
            await Task.Delay(_projectionOptions.DelayMs, cancellationToken);
        }

        var poll = await _pollRepository.GetByIdAsync(vote.PollId, cancellationToken)
            ?? throw new InvalidOperationException($"Poll {vote.PollId} not found for projection rebuild.");

        var loggedAtUtc = DateTime.UtcNow;
        var projection = await _projectionRepository.ApplyVoteAcceptedAsync(
            poll,
            vote,
            new VoteAuditLog
            {
                AuditLogId = Guid.NewGuid(),
                VoteId = vote.VoteId,
                PollId = vote.PollId,
                PollOptionId = vote.PollOptionId,
                UserId = vote.UserId ?? string.Empty,
                Architecture = architecture,
                Action = "VoteAccepted",
                LoggedAtUtc = loggedAtUtc
            },
            cancellationToken);

        return new PollResults
        {
            PollId = projection.PollId,
            PollTitle = projection.PollTitle,
            TotalVotes = projection.TotalVotes,
            LastUpdatedAtUtc = projection.LastUpdatedAtUtc,
            Options = projection.Options
                .OrderBy(o => o.OrderIndex)
                .Select(o => new PollResultOptionDto
                {
                    OptionId = o.PollOptionId,
                    OptionText = o.OptionText,
                    VoteCount = o.VoteCount
                })
                .ToList()
        };
    }

}
