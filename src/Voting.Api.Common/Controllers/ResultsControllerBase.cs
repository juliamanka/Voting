using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;

namespace Voting.Api.Common.Controllers;

public abstract class ResultsControllerBase : ControllerBase
{
    private readonly IPollService _pollService;

    protected ResultsControllerBase(IPollService pollService)
    {
        _pollService = pollService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PollResults>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetResults(CancellationToken cancellationToken)
    {
        var results = await _pollService.GetAllPollResults(cancellationToken);
        return Ok(results);
    }
}
