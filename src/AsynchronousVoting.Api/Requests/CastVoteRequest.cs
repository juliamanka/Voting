namespace AsynchronousVoting.Api.Requests;

public class CastVoteRequest
{
    public Guid PollId { get; set; }
    public Guid PollOptionId { get; set; }
    public string? UserId { get; set; }
}