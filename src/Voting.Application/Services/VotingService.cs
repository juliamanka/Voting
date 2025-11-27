using System.Diagnostics;
using AutoMapper;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Voting.Application.DTOs;
using Voting.Application.Exceptions;
using Voting.Application.Interfaces;
using Voting.Domain.Entities;
using Voting.Domain.Enums;
using Voting.Domain.Repository;

namespace Voting.Application.Services;

public class VotingService : IVotingService
{
   private readonly IVoteRepository _repository;
    private readonly IMapper _mapper;
    private readonly ILogger<VotingService> _logger;
    private readonly IPollRepository _pollRepository; 

    public VotingService(
        IVoteRepository repository, 
        IMapper mapper,
        ILogger<VotingService> logger,
        IPollRepository pollRepository
       )
    {
        _repository = repository;
        _mapper = mapper;
        _logger = logger;
        _pollRepository = pollRepository;
    }

    public async Task<VoteResponse> ProcessVoteAsync(VoteRequest voteRequest, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        { 
            var poll = await _pollRepository.GetByIdAsync(voteRequest.PollId, cancellationToken);
            var optionExists = poll.Options.Any(o => o.PollOptionId == voteRequest.PollOptionId);

            if (!optionExists)
            {
                throw new ValidationException("Chosen answer doesn't exist in the poll.");
            }
            
            if (poll == null)
            {
                throw new NotFoundException("Poll", voteRequest.PollId);
            }

            if (!poll.IsActive)
            {
                throw new PollInactiveException(voteRequest.PollId);
            }
            
            var voteRecord = _mapper.Map<VoteRecord>(voteRequest);
            
            var savedRecord = await _repository.AddVoteAsync(voteRecord, cancellationToken);
            
            stopwatch.Stop();

            var receipt = _mapper.Map<VoteResponse>(savedRecord);
            
            receipt.Status = VoteStatus.Counted;
            receipt.ServerProcessingTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation(
                "Vote successfuly saved: {VoteId} for poll {PollId} in {ProcessingTime}ms", 
                receipt.VoteId, 
                receipt.PollId, 
                receipt.ServerProcessingTimeMs);

            return receipt;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error for vote for: {PollId}", voteRequest.PollId);

            return new VoteResponse
            {
                PollId = voteRequest.PollId,
                Status = VoteStatus.Failed,
                ServerProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
             throw; 
        }
    }
}