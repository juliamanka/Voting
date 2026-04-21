namespace Voting.Domain.Entities;

public class PollOptionResultsProjection
{
    public Guid PollId { get; set; }

    public Guid PollOptionId { get; set; }

    public int OrderIndex { get; set; }

    public int VoteCount { get; set; }

    public string OptionText { get; set; } = string.Empty;

    public virtual PollResultsProjection PollResultsProjection { get; set; } = null!;
}
