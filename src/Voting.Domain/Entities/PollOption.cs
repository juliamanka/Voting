using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Voting.Domain.Entities;

public class PollOption
{
    [Key]
    public Guid PollOptionId { get; set; }

    [Required]
    public Guid PollId { get; set; } 

    [Required]
    [MaxLength(200)]
    public string Text { get; set; } 
    
    public int OrderIndex { get; set; }
    
    [JsonIgnore] 
    [ForeignKey(nameof(PollId))]
    public virtual Poll Poll { get; set; }

    public virtual ICollection<VoteRecord> Votes { get; set; }

    public PollOption()
    {
        Votes = new HashSet<VoteRecord>();
    }
}