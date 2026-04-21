namespace Voting.Application.DTOs;

public class Options
{
    public Guid OptionId { get; set; }
    
    public string OptionText { get; set; } = string.Empty;
    
    public int VoteCount { get; set; }
}
