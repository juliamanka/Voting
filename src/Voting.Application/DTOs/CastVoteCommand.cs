namespace Voting.Application.DTOs;

public record CastVoteCommand
{
    // Konstruktor bezparametrowy dla MassTransit/Serializatora
    public CastVoteCommand()
    {
    }

    public CastVoteCommand(
        Guid submissionId,
        Guid pollId,
        Guid pollOptionId,
        string? userId,
        DateTime requestStartedAtUtc,
        DateTime createdAtUtc)
    {
        SubmissionId = submissionId;
        PollId = pollId;
        PollOptionId = pollOptionId;
        UserId = userId;
        RequestStartedAtUtc = requestStartedAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid SubmissionId { get; init; }
    public Guid PollId { get; init; }
    public Guid PollOptionId { get; init; }
    public string? UserId { get; init; }
    public DateTime RequestStartedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
