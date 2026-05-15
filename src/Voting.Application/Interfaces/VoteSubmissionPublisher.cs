namespace Voting.Application.Interfaces;

public interface IVoteSubmissionPublisher
{
    Task<Guid> SubmitVoteAsync(Guid pollId, Guid optionId, string userId, CancellationToken cancellationToken = default);
}
