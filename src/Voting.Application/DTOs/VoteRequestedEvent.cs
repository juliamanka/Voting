namespace Voting.Application.DTOs;

public record VoteRequestedEvent(
    Guid PollId,
    Guid PollOptionId,
    DateTime TimeStamp);
