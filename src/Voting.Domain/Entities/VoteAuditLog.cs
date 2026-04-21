using System.ComponentModel.DataAnnotations;

namespace Voting.Domain.Entities;

public class VoteAuditLog
{
    [Key]
    public Guid AuditLogId { get; set; }

    public Guid VoteId { get; set; }

    public Guid PollId { get; set; }

    public Guid PollOptionId { get; set; }

    [MaxLength(256)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Architecture { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Action { get; set; } = string.Empty;

    public DateTime LoggedAtUtc { get; set; }
}
