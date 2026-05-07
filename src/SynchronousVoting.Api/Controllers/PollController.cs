using Microsoft.AspNetCore.Mvc;
using Voting.Api.Common.Controllers;
using Voting.Application.Interfaces;

namespace SynchronousVoting.Api.Controllers;

[ApiController]
[Route("api/polls")]
public sealed class PollController : PollControllerBase
{
    public PollController(IPollService pollService) : base(pollService)
    {
    }
}
