using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;

namespace Voting.Api.Common.Controllers;

public abstract class PollControllerBase : ControllerBase
{
    private readonly IPollService _pollService;

    protected PollControllerBase(IPollService pollService)
    {
        _pollService = pollService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PollDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivePolls(CancellationToken cancellationToken)
    {
        var polls = await _pollService.GetAvailablePollsAsync(cancellationToken);
        return Ok(polls);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PollDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPollById(Guid id, CancellationToken cancellationToken)
    {
        var poll = await _pollService.GetPollWithOptions(id, cancellationToken);
        return Ok(poll);
    }
}
