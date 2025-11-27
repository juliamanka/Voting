namespace Voting.Application.DTOs;

public record VoteRecordedEvent(
    Guid VoteId,
    Guid PollId,
    Guid PollOptionId,
    string? UserId,
    DateTime TimeStamp
);