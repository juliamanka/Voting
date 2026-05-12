using AutoMapper;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;
using Voting.Domain.Entities;
using Voting.Domain.Enums;
using Voting.Domain.Repository;

namespace Voting.Application.Services;

public class VoteWriteService : IVoteWriteService
{
    private readonly IVoteValidationService _voteValidationService;
    private readonly IVoteRepository _voteRepository;

    public VoteWriteService(
        IVoteValidationService voteValidationService,
        IVoteRepository voteRepository
        )
    {
        _voteValidationService = voteValidationService;
        _voteRepository = voteRepository;
    }

    public async Task<VoteRecord> WriteVoteAsync(VoteRequest voteRequest, CancellationToken cancellationToken)
    {
        await _voteValidationService.ValidateAsync(voteRequest, cancellationToken);

        var voteRecord = new VoteRecord
        {
            VoteId = Guid.NewGuid(),
            PollId = voteRequest.PollId,
            PollOptionId = voteRequest.PollOptionId,
            UserId = voteRequest.UserId,
            Status = VoteStatus.Counted,
            Timestamp = DateTime.UtcNow
        };
        
        voteRecord.Status = VoteStatus.Counted;

        return await _voteRepository.AddVoteAsync(voteRecord, cancellationToken);
    }
}
