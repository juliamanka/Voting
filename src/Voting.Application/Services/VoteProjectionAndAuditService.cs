using Voting.Application.DTOs;
using Voting.Application.Interfaces;
using Voting.Domain.Entities;
using Voting.Domain.Repository;

namespace Voting.Application.Services;

public class VoteProjectionAndAuditService : IVoteProjectionAndAuditService
{
    private readonly IPollRepository _pollRepository;
    private readonly IPollResultsProjectionRepository _projectionRepository;

    public VoteProjectionAndAuditService(
        IPollRepository pollRepository,
        IPollResultsProjectionRepository projectionRepository)
    {
        _pollRepository = pollRepository;
        _projectionRepository = projectionRepository;
    }

    public async Task<PollResults> ApplyVoteAcceptedAsync(
        VoteRecord vote,
        string architecture,
        CancellationToken cancellationToken)
    {
        var projectionDelayMs = ReadProjectionDelayMs();
        if (projectionDelayMs > 0)
        {
            await Task.Delay(projectionDelayMs, cancellationToken);
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
                .Select(o => new Options
                {
                    OptionId = o.PollOptionId,
                    OptionText = o.OptionText,
                    VoteCount = o.VoteCount
                })
                .ToList()
        };
    }

    private static int ReadProjectionDelayMs()
    {
        var raw = Environment.GetEnvironmentVariable("Chaos__ProjectionDelayMs")
                  ?? Environment.GetEnvironmentVariable("CHAOS_PROJECTION_DELAY_MS");
        return int.TryParse(raw, out var delayMs) && delayMs > 0 ? delayMs : 0;
    }
}
