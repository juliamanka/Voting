using Microsoft.AspNetCore.Mvc;
using Voting.Application.Interfaces;

namespace SynchronousVoting.Api.Controllers;

[ApiController]
[Route("api/results")]
public class ResultsController : ControllerBase
{
    private readonly IPollService _pollService;
    
    public ResultsController(IPollService pollService)
    {
        _pollService = pollService;
    }

    [HttpGet]
    public async Task<IActionResult> GetResults()
    {
        var results = await _pollService.GetAllVotesForPolls(CancellationToken.None);
       return Ok(results);
    }
}