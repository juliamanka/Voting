using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SynchronousVoting.Api.Monitoring;
using Voting.Api.Common.RequestTiming;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;

namespace SynchronousVoting.Api.Controllers;

[ApiController]
[Route("api/vote")]
[EnableRateLimiting("votes-policy")]
public class VoteController : ControllerBase
{
    private readonly IVotingService _votingService;

    public VoteController(IVotingService votingService)
    {
        _votingService = votingService;
    }

    /// <summary>
    /// Synchronicznie przetwarza i zapisuje głos.
    /// Klient czeka na pełne przetworzenie i zapis do bazy.
    /// </summary>
    /// <param name="request">Dane głosu (PollId, OptionId)</param>
    /// <param name="cancellationToken">Token do anulowania operacji</param>
    /// <returns>Potwierdzenie zapisu głosu (VoteReceipt)</returns>
    [HttpPost]
    [ProducesResponseType(typeof(VoteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SubmitVote(
        [FromBody] VoteRequest request,
        CancellationToken cancellationToken)
    {
        var receipt = await _votingService.ProcessVoteAsync(request, cancellationToken);
        var responseLatency = RequestTimingContext.GetElapsedSinceRequestStart(HttpContext);

        var tags = new KeyValuePair<string, object?>[]
        {
            new("architecture", "sync")
        };
        VotingMetrics.VoteProcessingDurationSeconds.Record(responseLatency.TotalSeconds, tags);
        VotingMetrics.VoteHttpResponseLatencySeconds.Record(responseLatency.TotalSeconds, tags);

        return CreatedAtAction(nameof(SubmitVote), new { id = receipt.VoteId }, receipt);
    }
}
