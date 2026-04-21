namespace Voting.Application.DTOs;

public class PollDto
{
    public Guid PollId { get; set; }
    public string Question { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool RequiresEligibilityCheck { get; set; }
    
    // Lista opcji odpowiedzi
    public List<PollOptionDto> Options { get; set; } = new();
}
