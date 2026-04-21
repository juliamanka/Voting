using System.ComponentModel.DataAnnotations;
using Voting.Domain.Enums;

namespace Voting.Domain.Entities;

public class VoteSubmission
{
    [Key]
    public Guid SubmissionId { get; set; }

    [Required]
    public Guid PollId { get; set; }

    [Required]
    public Guid PollOptionId { get; set; }

    [MaxLength(256)]
    public string? UserId { get; set; }

    [Required]
    [MaxLength(32)]
    public string Architecture { get; set; } = string.Empty;

    public VoteStatus Status { get; set; }

    [MaxLength(512)]
    public string? FailureReason { get; set; }

    public Guid? VoteId { get; set; }

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
