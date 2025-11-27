namespace Voting.Application.DTOs;

public class PollDto
{
    public Guid PollId { get; set; }
    public string Question { get; set; }
    public bool IsActive { get; set; }
    
    // Lista opcji odpowiedzi
    public List<PollOptionDto> Options { get; set; }
}