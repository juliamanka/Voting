namespace Voting.Application.Exceptions;

public class PollInactiveException : Exception
{
    public PollInactiveException(Guid pollId)
        : base($"Poll '{pollId}' is inactive.")
    {
    }
}