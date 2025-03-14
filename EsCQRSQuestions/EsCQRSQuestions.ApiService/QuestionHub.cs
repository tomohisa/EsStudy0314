using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Commands;
using Microsoft.AspNetCore.SignalR;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Orleans.Parts;

namespace EsCQRSQuestions.ApiService;

public class QuestionHub : Hub
{
    private readonly SekibanOrleansExecutor _executor;
    private static Guid? _activeUsersId;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    // Group names
    private const string AdminGroup = "Admins";
    private const string ParticipantGroup = "Participants";

    public QuestionHub(SekibanOrleansExecutor executor)
    {
        _executor = executor;
    }
    
    // Client connection
    public override async Task OnConnectedAsync()
    {
        // By default, add all clients to the Participants group
        await Groups.AddToGroupAsync(Context.ConnectionId, ParticipantGroup);
        
        // Track the active user
        await TrackUserConnection();
        
        await base.OnConnectedAsync();
    }
    
    // Client disconnection
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Track the user disconnection
        if (_activeUsersId.HasValue)
        {
            await _executor.CommandAsync(new UserDisconnectedCommand(
                _activeUsersId.Value,
                Context.ConnectionId));
        }
        
        await base.OnDisconnectedAsync(exception);
    }
    
    private async Task TrackUserConnection()
    {
        try
        {
            // Ensure we have an ActiveUsers aggregate
            await _semaphore.WaitAsync();
            try
            {
                if (!_activeUsersId.HasValue)
                {
                    var result = await _executor.CommandAsync(new CreateActiveUsersCommand());
                    if (result.IsSuccess)
                    {
                        _activeUsersId = result.GetValue().PartitionKeys.AggregateId;
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
            
            // Track the user connection
            if (_activeUsersId.HasValue)
            {
                string? name = null;
                if (Context.Items.TryGetValue("ParticipantName", out var nameObj) && nameObj is string nameStr)
                {
                    name = nameStr;
                }
                
                await _executor.CommandAsync(new UserConnectedCommand(
                    _activeUsersId.Value,
                    Context.ConnectionId,
                    name));
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error tracking user connection: {ex.Message}");
        }
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
        
        // Update the user name in the ActiveUsers aggregate
        if (_activeUsersId.HasValue && !string.IsNullOrEmpty(name))
        {
            try
            {
                await _executor.CommandAsync(new UpdateUserNameCommand(
                    _activeUsersId.Value,
                    Context.ConnectionId,
                    name));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error updating user name: {ex.Message}");
            }
        }
        
        await Clients.Caller.SendAsync("NameSet", name);
    }
}
