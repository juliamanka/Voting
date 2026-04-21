using System.ComponentModel.DataAnnotations;

namespace Voting.Domain.Entities;

public class PollResultsProjection
{
    [Key]
    public Guid PollId { get; set; }

    [Required]
    [MaxLength(500)]
    public string PollTitle { get; set; } = string.Empty;

    public int TotalVotes { get; set; }

    public DateTime LastUpdatedAtUtc { get; set; }

    public virtual ICollection<PollOptionResultsProjection> Options { get; set; } =
        new HashSet<PollOptionResultsProjection>();
}
