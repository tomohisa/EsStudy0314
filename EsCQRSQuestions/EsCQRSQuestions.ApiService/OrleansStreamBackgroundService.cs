using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events; // Added using for QuestionGroup events
using Microsoft.AspNetCore.SignalR;
using Orleans.Streams;
using Sekiban.Pure.Events;

namespace EsCQRSQuestions.ApiService;

public class OrleansStreamBackgroundService : BackgroundService
{
    private readonly IClusterClient _orleansClient;
    private readonly IHubNotificationService _hubService;
    private Orleans.Streams.IAsyncStream<IEvent>? _stream;
    private Orleans.Streams.StreamSubscriptionHandle<IEvent>? _subscriptionHandle;

    public OrleansStreamBackgroundService(
        IClusterClient orleansClient,
        IHubNotificationService hubService)
    {
        _orleansClient = orleansClient;
        _hubService = hubService;
    }

    public async Task OnNextAsync(IEvent item, StreamSequenceToken? token)
    {
        var eventType = item.GetPayload().GetType().Name;
        var aggregateId = item.PartitionKeys.AggregateId;
        
        // Handle different event types
        switch (item.GetPayload())
        {
            // Question events
            case QuestionCreated:
                await _hubService.NotifyAdminsAsync("QuestionCreated", new { AggregateId = aggregateId });
                break;
                
            case QuestionUpdated:
                await _hubService.NotifyAdminsAsync("QuestionUpdated", new { AggregateId = aggregateId });
                break;
                
            case QuestionDisplayStarted:
                // Notify both admins and participants when a question is displayed
                await _hubService.NotifyAllClientsAsync("QuestionDisplayStarted", new { AggregateId = aggregateId });
                break;
                
            case QuestionDisplayStopped:
                // Notify both admins and participants when a question is no longer displayed
                await _hubService.NotifyAllClientsAsync("QuestionDisplayStopped", new { AggregateId = aggregateId });
                break;
                
            case ResponseAdded responseAdded:
                // Notify both admins and participants when a response is added
                await _hubService.NotifyAllClientsAsync("ResponseAdded", new 
                { 
                    AggregateId = aggregateId,
                    ResponseId = responseAdded.ResponseId,
                    ParticipantName = responseAdded.ParticipantName,
                    SelectedOptionId = responseAdded.SelectedOptionId,
                    Comment = responseAdded.Comment,
                    Timestamp = responseAdded.Timestamp
                });
                break;
                
            case QuestionDeleted:
                await _hubService.NotifyAdminsAsync("QuestionDeleted", new { AggregateId = aggregateId });
                break;
                
            // ActiveUsers events
            case ActiveUsersCreated:
                await _hubService.NotifyAdminsAsync("ActiveUsersCreated", new { AggregateId = aggregateId });
                break;
                
            case UserConnected userConnected:
                await _hubService.NotifyAdminsAsync("UserConnected", new 
                { 
                    AggregateId = aggregateId,
                    ConnectionId = userConnected.ConnectionId,
                    Name = userConnected.Name,
                    ConnectedAt = userConnected.ConnectedAt
                });
                break;
                
            case UserDisconnected userDisconnected:
                await _hubService.NotifyAdminsAsync("UserDisconnected", new 
                { 
                    AggregateId = aggregateId,
                    ConnectionId = userDisconnected.ConnectionId,
                    DisconnectedAt = userDisconnected.DisconnectedAt
                });
                break;
                
            case UserNameUpdated userNameUpdated:
                await _hubService.NotifyAdminsAsync("UserNameUpdated", new 
                { 
                    AggregateId = aggregateId,
                    ConnectionId = userNameUpdated.ConnectionId,
                    Name = userNameUpdated.Name,
                    UpdatedAt = userNameUpdated.UpdatedAt
                });
                break;

            // QuestionGroup events
            case QuestionGroupCreated groupCreated:
                await _hubService.NotifyAdminsAsync("QuestionGroupCreated", new { AggregateId = aggregateId, Name = groupCreated.Name });
                break;

            case QuestionGroupUpdated groupUpdated:
                await _hubService.NotifyAdminsAsync("QuestionGroupUpdated", new { AggregateId = aggregateId, NewName = groupUpdated.NewName });
                break;

            case QuestionGroupDeleted groupDeleted:
                await _hubService.NotifyAdminsAsync("QuestionGroupDeleted", new { AggregateId = aggregateId });
                break;

            case QuestionAddedToGroup questionAdded:
                await _hubService.NotifyAdminsAsync("QuestionAddedToGroup", new { AggregateId = aggregateId, QuestionId = questionAdded.QuestionId, Order = questionAdded.Order });
                break;

            case QuestionRemovedFromGroup questionRemoved:
                await _hubService.NotifyAdminsAsync("QuestionRemovedFromGroup", new { AggregateId = aggregateId, QuestionId = questionRemoved.QuestionId });
                break;

            case QuestionOrderChanged orderChanged:
                await _hubService.NotifyAdminsAsync("QuestionOrderChanged", new { AggregateId = aggregateId, QuestionId = orderChanged.QuestionId, NewOrder = orderChanged.NewOrder });
                break;
                
            default:
                // For other event types, just log the event type
                Console.WriteLine($"Received event: {eventType} for aggregate {aggregateId}");
                break;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Get Orleans stream provider
        var streamProvider = _orleansClient.GetStreamProvider("EventStreamProvider");

        // Get stream with fixed StreamId
        _stream = streamProvider.GetStream<IEvent>(StreamId.Create("AllEvents", Guid.Empty));

        // Subscribe to the stream
        _subscriptionHandle = await _stream.SubscribeAsync(OnNextAsync, async ex =>
        {
            await _hubService.NotifyAdminsAsync("Error", new 
            { 
                Type = ex.GetType().Name, 
                Message = ex.Message 
            });
            await Task.CompletedTask;
        });
                
        // Wait until the service is cancelled
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscriptionHandle != null)
        {
            await _subscriptionHandle.UnsubscribeAsync();
        }
        await base.StopAsync(cancellationToken);
    }
}
