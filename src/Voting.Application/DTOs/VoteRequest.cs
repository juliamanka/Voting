using System.ComponentModel.DataAnnotations;

namespace Voting.Application.DTOs;

public class VoteRequest
{
    [Required]
    public Guid PollId { get; set; }

    [Required]
    public Guid PollOptionId { get; set; }
    
    public string? UserId { get; set; }
}