namespace Voting.Application.Exceptions;

public class IneligibleVoterException : Exception
{
    public IneligibleVoterException(string userId)
        : base($"User '{userId}' is not eligible to vote.")
    {
        UserId = userId;
    }

    public string UserId { get; }
}
