namespace Voting.Application.DTOs;

public record VoteRecordedEvent(
    Guid VoteId,
    Guid PollId,
    Guid PollOptionId,
    string? UserId,
    DateTime VoteRecordedAtUtc,
    DateTime RequestStartedAtUtc,
    DateTime PublishedAtUtc)
{
    // Populated by the async Stage 1 consumer for end-to-end metric calculation in Stage 2.
    // Null in the hybrid path (hybrid API publishes this event directly without a prior worker stage).
    public DateTime? BrokerSentAtUtc { get; init; }
    public DateTime? Stage1WorkerStartedAtUtc { get; init; }
}
