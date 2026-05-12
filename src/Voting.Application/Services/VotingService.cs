using System.Diagnostics;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;
using Voting.Domain.Enums;

namespace Voting.Application.Services;

public class VotingService : IVotingService
{
    private readonly ILogger<VotingService> _logger;
    private readonly IVoteWriteService _voteWriteService;
    private readonly IVoteProjectionAndAuditService _voteProjectionAndAuditService;

    public VotingService(
        ILogger<VotingService> logger,
        IVoteWriteService voteWriteService,
        IVoteProjectionAndAuditService voteProjectionAndAuditService
       )
    {
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
        var voteResponse = new VoteResponse
        {
            VoteId = savedRecord.VoteId,
            PollId = savedRecord.PollId,
            Timestamp = savedRecord.Timestamp
        };
        
        voteResponse.Status = VoteStatus.Counted;
        voteResponse.ServerProcessingTimeMs = stopwatch.ElapsedMilliseconds;

        _logger.LogInformation(
            "Vote successfully saved: {VoteId} for poll {PollId} in {ProcessingTime}ms",
            voteResponse.VoteId,
            voteResponse.PollId,
            voteResponse.ServerProcessingTimeMs);

        return new VoteProcessingResult(voteResponse, voteCommittedAtUtc, projectionCompletedAtUtc);
    }
}
