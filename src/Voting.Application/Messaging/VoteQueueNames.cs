namespace Voting.Application.Messaging;

public static class VoteQueueNames
{
    public const string AsyncCastVoteQueue = "cast-vote-queue";
    public const string AsyncVoteRecordedEventsQueue = "async-vote-recorded-events";
    public const string AsyncPollResultsUpdatedEventsQueue = "async-poll-results-updated-events";
    public const string HybridPollResultsUpdatedEventsQueue = "hybrid-poll-results-updated-events";
    public const string HybridVoteRecordedEvenetsQueue = "hybrid-vote-recorded-events";
    
    
}
