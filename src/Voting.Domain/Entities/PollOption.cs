using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Voting.Domain.Entities;

public class PollOption
{
    [Key]
    public Guid PollOptionId { get; set; }

    [Required]
    public Guid PollId { get; set; } // Klucz obcy do Ankiety

    [Required]
    [MaxLength(200)]
    public string Text { get; set; } // Treść opcji, np. "Tak", "Nie"

    /// <summary>
    /// Kolejność wyświetlania opcji na froncie (np. 1, 2, 3).
    /// </summary>
    public int OrderIndex { get; set; }

    // === Relacje ===
    
    [JsonIgnore] // Zapobiega cyklom przy serializacji
    [ForeignKey(nameof(PollId))]
    public virtual Poll Poll { get; set; }

    // Opcjonalnie: lista głosów oddanych na TĘ konkretną opcję
    public virtual ICollection<VoteRecord> Votes { get; set; }

    public PollOption()
    {
        Votes = new HashSet<VoteRecord>();
    }
}