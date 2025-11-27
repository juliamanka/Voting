using System.ComponentModel.DataAnnotations;

namespace Voting.Domain.Entities;

public class Poll
{
    [Key]
    public Guid PollId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Question { get; set; }

    /// <summary>
    /// Lista dostępnych opcji odpowiedzi (Relacja 1-N).
    /// </summary>
    [Required]
    public virtual ICollection<PollOption> Options { get; set; }
    
    /// <summary>
    /// Czy ankieta jest obecnie aktywna i można na nią głosować.
    /// </summary>
    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    // === Właściwość Nawigacyjna ===
    
    /// <summary>
    /// Kolekcja wszystkich głosów oddanych w tej ankiecie.
    /// </summary>
    public virtual ICollection<VoteRecord> Votes { get; set; }

    public Poll()
    {
        // Dobra praktyka: inicjalizuj kolekcje, aby uniknąć null reference
        Votes = new HashSet<VoteRecord>();
        Options = new HashSet<PollOption>();
        CreatedAt = DateTime.UtcNow;
        IsActive = true;
    }
}