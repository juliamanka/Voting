using System.ComponentModel.DataAnnotations;

namespace Voting.Domain.Entities;

public class VoterEligibility
{
    [Key]
    [MaxLength(256)]
    public string UserId { get; set; } = string.Empty;

    public bool IsEligible { get; set; }

    [MaxLength(64)]
    public string EligibilitySource { get; set; } = string.Empty;

    public int ChecksPerformed { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime LastCheckedAtUtc { get; set; }
}
