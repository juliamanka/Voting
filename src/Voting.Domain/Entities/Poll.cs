using System.ComponentModel.DataAnnotations;

namespace Voting.Domain.Entities;

public class Poll
{
    [Key]
    public Guid PollId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Question { get; set; } = string.Empty;

    [Required]
    public virtual ICollection<PollOption> Options { get; set; } = new HashSet<PollOption>();

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<VoteRecord> Votes { get; set; } = new HashSet<VoteRecord>();

    public Poll()
    {
        CreatedAt = DateTime.UtcNow;
        IsActive = true;
    }
}
