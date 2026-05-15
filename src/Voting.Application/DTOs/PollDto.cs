namespace Voting.Application.DTOs;

public class PollDto
{
    public Guid PollId { get; set; }
    public string Question { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public List<PollOptionDto> Options { get; set; } = new();
}
