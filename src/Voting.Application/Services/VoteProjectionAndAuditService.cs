using Voting.Application.DTOs;
using Voting.Application.Interfaces;
using Voting.Domain.Entities;
using Voting.Domain.Repository;

namespace Voting.Application.Services;

public class VoteProjectionAndAuditService : IVoteProjectionAndAuditService
{
    private readonly IPollRepository _pollRepository;
    private readonly IPollResultsProjectionRepository _projectionRepository;
    private readonly IVoteAuditLogRepository _auditLogRepository;

    public VoteProjectionAndAuditService(
        IPollRepository pollRepository,
        IPollResultsProjectionRepository projectionRepository,
        IVoteAuditLogRepository auditLogRepository)
    {
        _pollRepository = pollRepository;
        _projectionRepository = projectionRepository;
        _auditLogRepository = auditLogRepository;
    }

    public async Task<PollResults> ApplyVoteAcceptedAsync(
        VoteRecord vote,
        string architecture,
        CancellationToken cancellationToken)
    {
        var poll = await _pollRepository.GetByIdAsync(vote.PollId, cancellationToken)
            ?? throw new InvalidOperationException($"Poll {vote.PollId} not found for projection update.");

        var projection = await _projectionRepository.GetOrCreateAsync(poll, cancellationToken);
        var optionProjection = projection.Options.FirstOrDefault(o => o.PollOptionId == vote.PollOptionId)
            ?? throw new InvalidOperationException(
                $"Projection option {vote.PollOptionId} not found for poll {vote.PollId}.");

        optionProjection.VoteCount += 1;
        projection.TotalVotes += 1;
        projection.LastUpdatedAtUtc = DateTime.UtcNow;

        await _auditLogRepository.AddAsync(
            new VoteAuditLog
            {
                AuditLogId = Guid.NewGuid(),
                VoteId = vote.VoteId,
                PollId = vote.PollId,
                PollOptionId = vote.PollOptionId,
                UserId = vote.UserId ?? string.Empty,
                Architecture = architecture,
                Action = "VoteAccepted",
                LoggedAtUtc = projection.LastUpdatedAtUtc
            },
            cancellationToken);

        await _projectionRepository.SaveChangesAsync(cancellationToken);

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
