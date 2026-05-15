using FluentValidation;
using Voting.Application.DTOs;
using Voting.Application.Exceptions;
using Voting.Application.Interfaces;
using Voting.Domain.Repository;

namespace Voting.Application.Services;

public class VoteValidationService : IVoteValidationService
{
    private readonly IValidator<VoteRequest> _validator;
    private readonly IPollRepository _pollRepository;
    private readonly IVoteRepository _voteRepository;

    public VoteValidationService(
        IValidator<VoteRequest> validator,
        IPollRepository pollRepository,
        IVoteRepository voteRepository)
    {
        _validator = validator;
        _pollRepository = pollRepository;
        _voteRepository = voteRepository;
    }

    public async Task ValidateAsync(VoteRequest voteRequest, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(voteRequest, cancellationToken);

        var poll = await _pollRepository.GetByIdAsync(voteRequest.PollId, cancellationToken);
        if (poll is null)
        {
            throw new NotFoundException("Poll", voteRequest.PollId);
        }

        if (!poll.IsActive)
        {
            throw new PollInactiveException(voteRequest.PollId);
        }

        var optionExists = poll.Options.Any(o => o.PollOptionId == voteRequest.PollOptionId);
        if (!optionExists)
        {
            throw new ValidationException("Chosen answer does not exist in the poll.");
        }

        if (!string.IsNullOrWhiteSpace(voteRequest.UserId) &&
            await _voteRepository.HasUserVotedAsync(voteRequest.PollId, voteRequest.UserId, cancellationToken))
        {
            throw new DuplicateVoteException(voteRequest.PollId, voteRequest.UserId);
        }
    }
}
