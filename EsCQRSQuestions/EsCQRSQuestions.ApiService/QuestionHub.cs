using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Commands;
using Microsoft.AspNetCore.SignalR;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Orleans.Parts;

namespace EsCQRSQuestions.ApiService;

public class QuestionHub : Hub
{
    private readonly SekibanOrleansExecutor _executor;
    
    // Fixed aggregate ID for the ActiveUsers aggregate
    private static readonly Guid _activeUsersId = Guid.Parse("0195a6f7-dfff-75a7-b99f-36a0552a8eca");
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private static bool _activeUsersCreated = false;

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
        try
        {
            // By default, add all clients to the Participants group
            await Groups.AddToGroupAsync(Context.ConnectionId, ParticipantGroup);
            
            // 管理者接続時はActiveUsersに追加しない
            // TrackUserConnectionは参加者専用のメソッドとして残す
            
            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error in OnConnectedAsync: {ex.Message}");
            throw;
        }
    }
    
    // Client disconnection
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            // Track the user disconnection - only if already in ActiveUsers
            await _executor.CommandAsync(new UserDisconnectedCommand(
                _activeUsersId,
                Context.ConnectionId));
            
            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error in OnDisconnectedAsync: {ex.Message}");
            // Don't rethrow - we should allow disconnection even if tracking fails
        }
    }
    
    private async Task EnsureActiveUsersExists()
    {
        if (_activeUsersCreated)
        {
            return;
        }
        
        // Ensure we have an ActiveUsers aggregate
        await _semaphore.WaitAsync();
        try
        {
            if (!_activeUsersCreated)
            {
                // Create the ActiveUsers aggregate with the fixed ID if it doesn't exist
                await _executor.CommandAsync(new CreateActiveUsersCommand());
                _activeUsersCreated = true;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    private async Task TrackUserConnection()
    {
        try
        {
            await EnsureActiveUsersExists();
            
            // Track the user connection
            string? name = null;
            if (Context.Items.TryGetValue("ParticipantName", out var nameObj) && nameObj is string nameStr)
            {
                name = nameStr;
            }
            
            await _executor.CommandAsync(new UserConnectedCommand(
                _activeUsersId,
                Context.ConnectionId,
                name));
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
        
        // 管理者グループに参加したユーザーはActiveUsersから削除
        try
        {
            await _executor.CommandAsync(new UserDisconnectedCommand(
                _activeUsersId,
                Context.ConnectionId));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error removing admin from ActiveUsers: {ex.Message}");
            // Continue even if there's an error
        }
    }
    
    // 参加者専用のメソッドを追加
    public async Task JoinAsSurveyParticipant()
    {
        // 参加者としてActiveUsersに追加
        await TrackUserConnection();
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
        if (!string.IsNullOrEmpty(name))
        {
            try
            {
                await _executor.CommandAsync(new UpdateUserNameCommand(
                    _activeUsersId,
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
