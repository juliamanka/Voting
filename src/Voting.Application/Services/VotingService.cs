using System.Diagnostics;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;
using Voting.Domain.Entities;
using Voting.Domain.Enums;
using Voting.Domain.Repository;

namespace Voting.Application.Services;

public class VotingService : IVotingService
{
    private readonly IMapper _mapper;
    private readonly ILogger<VotingService> _logger;
    private readonly IVoteWriteService _voteWriteService;
    private readonly IVoteProjectionAndAuditService _voteProjectionAndAuditService;

    public VotingService(
        IMapper mapper,
        ILogger<VotingService> logger,
        IVoteWriteService voteWriteService,
        IVoteProjectionAndAuditService voteProjectionAndAuditService
       )
    {
        _mapper = mapper;
        _logger = logger;
        _voteWriteService = voteWriteService;
        _voteProjectionAndAuditService = voteProjectionAndAuditService;
    }

    public async Task<VoteProcessingResult> ProcessVoteAsync(VoteRequest voteRequest, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var savedRecord = await _voteWriteService.WriteVoteAsync(voteRequest, cancellationToken);
        var voteCommittedAtUtc = DateTime.UtcNow;
        await _voteProjectionAndAuditService.ApplyVoteAcceptedAsync(savedRecord, "sync", cancellationToken);
        var projectionCompletedAtUtc = DateTime.UtcNow;

        stopwatch.Stop();

        var receipt = _mapper.Map<VoteResponse>(savedRecord);
        receipt.Status = VoteStatus.Counted;
        receipt.ServerProcessingTimeMs = stopwatch.ElapsedMilliseconds;

        _logger.LogInformation(
            "Vote successfully saved: {VoteId} for poll {PollId} in {ProcessingTime}ms",
            receipt.VoteId,
            receipt.PollId,
            receipt.ServerProcessingTimeMs);

        return new VoteProcessingResult(receipt, voteCommittedAtUtc, projectionCompletedAtUtc);
    }
}
