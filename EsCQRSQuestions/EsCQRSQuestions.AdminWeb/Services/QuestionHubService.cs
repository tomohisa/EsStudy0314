using Microsoft.AspNetCore.SignalR.Client;
using EsCQRSQuestions.AdminWeb.Models;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace EsCQRSQuestions.AdminWeb.Services
{
    public class QuestionHubService : IAsyncDisposable
    {
        private readonly IHttpMessageHandlerFactory _httpMessageHandlerFactory;
        private readonly ILogger<QuestionHubService> _logger;
        private readonly string _hubUrl;
        private HubConnection? _hubConnection;
        
        // Events that components can subscribe to
        public event Func<Task>? ActiveUsersChanged;
        public event Func<Task>? QuestionChanged;
        public event Func<Task>? QuestionGroupChanged;
        public event Func<Task>? ResponseAdded;

        public QuestionHubService(
            NavigationManager navigationManager,
            IHttpMessageHandlerFactory httpMessageHandlerFactory,
            ILogger<QuestionHubService> logger)
        {
            _httpMessageHandlerFactory = httpMessageHandlerFactory;
            _logger = logger;
            _hubUrl = "https+http://apiservice/questionHub";
        }

        public async Task InitializeAsync()
        {
            if (_hubConnection != null)
            {
                return;
            }
            
            _hubConnection = new HubConnectionBuilder()
                .WithUrlWithClientFactory(_hubUrl, _httpMessageHandlerFactory)
                .WithAutomaticReconnect(new[] { 
                    TimeSpan.FromSeconds(1), 
                    TimeSpan.FromSeconds(2), 
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10) 
                })
                .ConfigureLogging(logging => {
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Debug);
                })
                .Build();

            RegisterHubEvents();
            RegisterReconnectionHandlers();

            try
            {
                _logger.LogInformation("Starting SignalR connection...");
                
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                var connectionTask = _hubConnection.StartAsync();
                
                var completedTask = await Task.WhenAny(connectionTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException("SignalR connection timed out after 30 seconds");
                }
                
                await connectionTask;
                
                _logger.LogInformation($"SignalR connection established successfully. State: {_hubConnection.State}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting SignalR connection: {ex.Message}");
                _logger.LogError(ex.StackTrace);
            }
        }

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public async Task JoinAdminGroup()
        {
            if (IsConnected)
            {
                await _hubConnection!.InvokeAsync("JoinAdminGroup");
            }
        }

        // UniqueCodeを指定して表示依頼を送信
        public async Task StartDisplayQuestionForGroup(Guid questionId, string uniqueCode)
        {
            if (IsConnected && !string.IsNullOrWhiteSpace(uniqueCode))
            {
                await _hubConnection!.InvokeAsync("StartDisplayQuestionForGroup", questionId, uniqueCode);
            }
        }

        private void RegisterHubEvents()
        {
            if (_hubConnection == null)
                return;

            // Active users events
            _hubConnection.On<object>("ActiveUsersCreated", _ => 
                OnActiveUsersChanged());
            
            _hubConnection.On<object>("UserConnected", _ => 
                OnActiveUsersChanged());
            
            _hubConnection.On<object>("UserDisconnected", _ => 
                OnActiveUsersChanged());
            
            _hubConnection.On<object>("UserNameUpdated", _ => 
                OnActiveUsersChanged());
            
            // Question events
            _hubConnection.On<object>("QuestionCreated", _ => 
                OnQuestionChanged());

            _hubConnection.On<object>("QuestionUpdated", _ => 
                OnQuestionChanged());

            _hubConnection.On<object>("QuestionDisplayStarted", _ => 
                OnQuestionChanged());

            _hubConnection.On<object>("QuestionDisplayStopped", _ => 
                OnQuestionChanged());

            _hubConnection.On<object>("QuestionDeleted", _ => 
                OnQuestionChanged());

            // Response events
            _hubConnection.On<object>("ResponseAdded", _ => 
                OnResponseAdded());

            // Question group events
            _hubConnection.On<object>("QuestionGroupCreated", _ => 
                OnQuestionGroupChanged());

            _hubConnection.On<object>("QuestionGroupUpdated", _ => 
                OnQuestionGroupChanged());

            _hubConnection.On<object>("QuestionGroupDeleted", _ => 
                OnQuestionGroupChanged());

            _hubConnection.On<object>("QuestionAddedToGroup", _ => 
                OnQuestionGroupChanged());

            _hubConnection.On<object>("QuestionRemovedFromGroup", _ => 
                OnQuestionGroupChanged());

            _hubConnection.On<object>("QuestionOrderChanged", _ => 
                OnQuestionGroupChanged());
        }

        private void RegisterReconnectionHandlers()
        {
            if (_hubConnection == null)
                return;

            _hubConnection.Closed += async (error) =>
            {
                _logger.LogError($"SignalR connection closed: {error?.Message}");
                _logger.LogError($"Connection state: {_hubConnection?.State}");
                
                for (var i = 0; i < 5; i++)
                {
                    try
                    {
                        var retryDelay = Math.Min(1000 * Math.Pow(2, i), 30000);
                        _logger.LogInformation($"Attempting reconnection in {retryDelay/1000} seconds...");
                        await Task.Delay((int)retryDelay);
                        
                        if (_hubConnection is not null)
                        {
                            await _hubConnection.StartAsync();
                            _logger.LogInformation("SignalR connection restarted successfully");
                            
                            await _hubConnection.InvokeAsync("JoinAdminGroup");
                            
                            // Notify subscribers that they need to refresh their data
                            OnActiveUsersChanged();
                            OnQuestionChanged();
                            OnQuestionGroupChanged();
                            
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error during reconnection attempt {i+1}: {ex.Message}");
                        if (i == 4)
                        {
                            _logger.LogError("Failed to reconnect after multiple attempts");
                        }
                    }
                }
            };

            _hubConnection.Reconnecting += (error) =>
            {
                _logger.LogInformation($"SignalR reconnecting... Error: {error?.Message}");
                _logger.LogInformation($"Connection state: {_hubConnection?.State}");
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += (connectionId) =>
            {
                _logger.LogInformation($"SignalR reconnected with connection ID: {connectionId}");
                _logger.LogInformation($"Connection state: {_hubConnection?.State}");
                return Task.CompletedTask;
            };
        }

        private Task OnActiveUsersChanged()
        {
            _logger.LogInformation("Active users changed event received");
            return ActiveUsersChanged?.Invoke() ?? Task.CompletedTask;
        }

        private Task OnQuestionChanged()
        {
            _logger.LogInformation("Question changed event received");
            return QuestionChanged?.Invoke() ?? Task.CompletedTask;
        }

        private Task OnQuestionGroupChanged()
        {
            _logger.LogInformation("Question group changed event received");
            return QuestionGroupChanged?.Invoke() ?? Task.CompletedTask;
        }

        private Task OnResponseAdded()
        {
            _logger.LogInformation("Response added event received");
            return ResponseAdded?.Invoke() ?? Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
            }
        }
    }
}