using Microsoft.AspNetCore.SignalR;

namespace Asynchronous.Api.Hubs;

public class ResultsHub : Hub
{
    public Task JoinPollGroup(Guid pollId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, pollId.ToString());
    }

    public override Task OnConnectedAsync()
    {
        Console.WriteLine($"Client connected: {Context.ConnectionId}");
        return base.OnConnectedAsync();
    }
}