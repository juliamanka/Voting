using Microsoft.AspNetCore.SignalR;

namespace SynchronousVoting.Api.Hubs;

public class ResultsHub : Hub
{
    public Task JoinPollGroup(Guid pollId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, pollId.ToString());
    }
}
