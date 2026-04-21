using Voting.Domain.Enums;

namespace Voting.Application.DTOs;

public class VoteAcceptedResponse
{
    public Guid SubmissionId { get; set; }
    public Guid PollId { get; set; }
    public Guid PollOptionId { get; set; }
    public VoteStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
}
