using Microsoft.AspNetCore.Mvc;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;

namespace AsynchronousVoting.Api.Controllers;

[ApiController]
[Route("api/polls")] 
public class PollController : ControllerBase
{
    private readonly IPollService _pollService;

    public PollController(IPollService pollService)
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
    public async Task<IActionResult> GetPollById(Guid id)
    {   
        var poll = await _pollService.GetPollWithOptions(id, new CancellationToken());
        return Ok(poll);
    }
}
