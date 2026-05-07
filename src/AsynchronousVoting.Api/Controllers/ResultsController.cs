using Microsoft.AspNetCore.Mvc;
using Voting.Api.Common.Controllers;
using Voting.Application.Interfaces;

namespace AsynchronousVoting.Api.Controllers;

[ApiController]
[Route("api/results")]
public sealed class ResultsController : ResultsControllerBase
{
    public ResultsController(IPollService pollService) : base(pollService)
    {
    }
}
