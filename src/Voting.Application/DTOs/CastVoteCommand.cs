namespace Voting.Application.DTOs;

public record CastVoteCommand(
    Guid PollId,
    Guid PollOptionId,
    string? UserId,
    DateTime CreatedAtUtc
);