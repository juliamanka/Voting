using System.ComponentModel.DataAnnotations;

namespace Voting.Domain.Entities;

public class Poll
{
    [Key]
    public Guid PollId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Question { get; set; }

    [Required]
    public virtual ICollection<PollOption> Options { get; set; }

    public bool IsActive { get; set; }

    public bool RequiresEligibilityCheck { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<VoteRecord> Votes { get; set; }

    public Poll()
    {
        Votes = new HashSet<VoteRecord>();
        Options = new HashSet<PollOption>();
        CreatedAt = DateTime.UtcNow;
        IsActive = true;
        RequiresEligibilityCheck = true;
    }
}
