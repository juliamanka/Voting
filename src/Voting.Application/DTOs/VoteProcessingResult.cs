namespace Voting.Application.DTOs;

public sealed record VoteProcessingResult(
    VoteResponse Response,
    DateTime VoteCommittedAtUtc,
    DateTime ProjectionCompletedAtUtc);
