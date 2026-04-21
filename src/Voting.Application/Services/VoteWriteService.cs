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
    private readonly IMapper _mapper;

    public VoteWriteService(
        IVoteValidationService voteValidationService,
        IVoteRepository voteRepository,
        IMapper mapper)
    {
        _voteValidationService = voteValidationService;
        _voteRepository = voteRepository;
        _mapper = mapper;
    }

    public async Task<VoteRecord> WriteVoteAsync(VoteRequest voteRequest, CancellationToken cancellationToken)
    {
        await _voteValidationService.ValidateAsync(voteRequest, cancellationToken);

        var voteRecord = _mapper.Map<VoteRecord>(voteRequest);
        voteRecord.Status = VoteStatus.Counted;

        return await _voteRepository.AddVoteAsync(voteRecord, cancellationToken);
    }
}
