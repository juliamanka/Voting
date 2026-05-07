namespace Voting.Application.DTOs;

public record PollResultsUpdatedEvent(
    PollResults PollResults,
    DateTime RequestStartedAtUtc,
    DateTime PublishedAtUtc);
