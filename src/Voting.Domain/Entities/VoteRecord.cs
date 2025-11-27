using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Voting.Domain.Entities;

public class VoteRecord
{
    [Key]
    public Guid VoteId { get; set; }

    [Required]
    public Guid PollId { get; set; }

    [Required]
    public Guid PollOptionId { get; set; }
    
    [MaxLength(256)]
    public string? UserId { get; set; }

    public DateTime Timestamp { get; set; }
    
    [ForeignKey(nameof(PollId))] 
    public virtual Poll Poll { get; set; }
    
    [ForeignKey(nameof(PollOptionId))]
    public virtual PollOption PollOption { get; set; }
}