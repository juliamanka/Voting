using Voting.Domain.Enums;

namespace Voting.Application.DTOs;

public class VoteSubmissionStatusResponse
{
    public Guid SubmissionId { get; set; }
    public Guid PollId { get; set; }
    public Guid PollOptionId { get; set; }
    public string? UserId { get; set; }
    public string Architecture { get; set; } = string.Empty;
    public VoteStatus Status { get; set; }
    public Guid? VoteId { get; set; }
    public string? FailureReason { get; set; }
    public DateTime RequestStartedAtUtc { get; set; }
    public DateTime AcceptedAtUtc { get; set; }
    public DateTime? BrokerSentAtUtc { get; set; }
    public DateTime? WorkerStartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public long? HttpResponseLatencyMs { get; set; }
    public long? QueueDelayMs { get; set; }
    public long? WorkerExecutionLatencyMs { get; set; }
    public long? EndToEndLatencyMs { get; set; }
}
