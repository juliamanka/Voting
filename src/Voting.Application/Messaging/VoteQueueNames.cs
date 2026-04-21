namespace Voting.Application.Messaging;

public static class VoteQueueNames
{
    public const string AsyncCastVoteQueue = "cast-vote-queue";
    public const string HybridCastVoteQueue = "hybrid-cast-vote-queue";
}
