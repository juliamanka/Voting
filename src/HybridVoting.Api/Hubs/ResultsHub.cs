using Microsoft.AspNetCore.SignalR;

namespace HybridVoting.Api.Hubs;

public class ResultsHub : Hub
{
    public Task JoinPollGroup(Guid pollId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, pollId.ToString());
    }
}
