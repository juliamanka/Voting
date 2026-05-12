namespace Voting.Application.DTOs;

public class PollResults
{
    public Guid PollId { get; set; }
    
    public string PollTitle { get; set; } = string.Empty;

    public int TotalVotes { get; set; }

    public DateTime LastUpdatedAtUtc { get; set; }
    
    public List<PollResultOptionDto> Options { get; set; } = new();
}
