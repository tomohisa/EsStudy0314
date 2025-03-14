using Microsoft.AspNetCore.SignalR;

namespace EsCQRSQuestions.ApiService;

public class QuestionHub : Hub
{
    // Group names
    private const string AdminGroup = "Admins";
    private const string ParticipantGroup = "Participants";
    
    // Client connection
    public override async Task OnConnectedAsync()
    {
        // By default, add all clients to the Participants group
        await Groups.AddToGroupAsync(Context.ConnectionId, ParticipantGroup);
        await base.OnConnectedAsync();
    }
    
    // Join admin group
    public async Task JoinAdminGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
    }
    
    // Leave admin group
    public async Task LeaveAdminGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AdminGroup);
    }
    
    // Set participant name
    public async Task SetParticipantName(string name)
    {
        // Store the participant name in the connection context
        Context.Items["ParticipantName"] = name;
        await Clients.Caller.SendAsync("NameSet", name);
    }
}
