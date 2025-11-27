namespace Voting.Application.DTOs;

public class PollOptionDto
{
    public Guid PollOptionId { get; set; }
    public string Text { get; set; }
    public int OrderIndex { get; set; }
}