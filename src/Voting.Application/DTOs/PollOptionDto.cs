namespace Voting.Application.DTOs;

public class PollOptionDto
{
    public Guid PollOptionId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
}
