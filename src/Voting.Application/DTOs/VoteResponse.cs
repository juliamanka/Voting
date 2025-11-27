using Voting.Domain.Enums;

namespace Voting.Application.DTOs;

public class VoteResponse
{
    public Guid VoteId { get; set; }
    public Guid PollId { get; set; }
    public VoteStatus Status { get; set; }
    public DateTime Timestamp { get; set; }
    
    // Metric used for analytics and performance monitoring
    public long ServerProcessingTimeMs { get; set; }
}