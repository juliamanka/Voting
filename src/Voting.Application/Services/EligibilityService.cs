using Voting.Application.Exceptions;
using Voting.Application.Interfaces;
using Voting.Domain.Repository;

namespace Voting.Application.Services;

public class EligibilityService : IEligibilityService
{
    private readonly IVoterEligibilityRepository _eligibilityRepository;

    public EligibilityService(IVoterEligibilityRepository eligibilityRepository)
    {
        _eligibilityRepository = eligibilityRepository;
    }

    public async Task EnsureEligibleAsync(string userId, CancellationToken cancellationToken)
    {
        var eligibility = await _eligibilityRepository.GetOrCreateAsync(userId, cancellationToken);

        eligibility.ChecksPerformed += 1;
        eligibility.LastCheckedAtUtc = DateTime.UtcNow;

        await _eligibilityRepository.SaveChangesAsync(cancellationToken);

        if (!eligibility.IsEligible)
        {
            throw new IneligibleVoterException(userId);
        }
    }
}
