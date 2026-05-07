using Voting.Domain.Enums;

namespace Voting.Application.DTOs;

public class VoteResponse
{
    public Guid VoteId { get; set; }
    public Guid PollId { get; set; }
    public VoteStatus Status { get; set; }
    public DateTime Timestamp { get; set; }

    public long ServerProcessingTimeMs { get; set; }
}
