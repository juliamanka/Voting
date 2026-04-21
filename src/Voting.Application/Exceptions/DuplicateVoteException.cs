namespace Voting.Application.Exceptions;

public class DuplicateVoteException : Exception
{
    public DuplicateVoteException(Guid pollId, string? userId)
        : base($"User '{userId}' has already voted in poll '{pollId}'.")
    {
    }
}
