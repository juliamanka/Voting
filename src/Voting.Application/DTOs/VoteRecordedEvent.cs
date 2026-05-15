namespace Voting.Application.DTOs;

public record VoteRecordedEvent(
    Guid VoteId,
    Guid PollId,
    Guid PollOptionId,
    string UserId,
    DateTime VoteRecordedAtUtc,
    DateTime RequestStartedAtUtc,
    DateTime PublishedAtUtc)
{
    public DateTime? BrokerSentAtUtc { get; init; }
    public DateTime? Stage1WorkerStartedAtUtc { get; init; }
}
