namespace Voting.Application.Interfaces;

public interface IEligibilityService
{
    Task EnsureEligibleAsync(string userId, CancellationToken cancellationToken);
}
