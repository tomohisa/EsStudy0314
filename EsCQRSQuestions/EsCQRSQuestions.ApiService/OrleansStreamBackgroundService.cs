using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using EsCQRSQuestions.Domain.Aggregates.Questions;
using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using Orleans.Streams;
using ResultBoxes;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;
using Sekiban.Pure.Orleans;

// Added using for QuestionGroup events

namespace EsCQRSQuestions.ApiService;

public class OrleansStreamBackgroundService : BackgroundService
{
    private readonly IClusterClient _orleansClient;
    private readonly IHubNotificationService _hubService;
    private readonly SekibanOrleansExecutor _sekibanOrleansExecutor;
    private IAsyncStream<IEvent>? _stream;
    private StreamSubscriptionHandle<IEvent>? _subscriptionHandle;

    public OrleansStreamBackgroundService(
        IClusterClient orleansClient,
        IHubNotificationService hubService,
        SekibanOrleansExecutor sekibanOrleansExecutor)
    {
        _orleansClient = orleansClient;
        _hubService = hubService;
        _sekibanOrleansExecutor = sekibanOrleansExecutor;
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
            case ResponseAdded responseAdded:
                // Notify both admins and participants when a response is added
                await _hubService.NotifyAdminsAsync("ResponseAdded", new
                {
                    AggregateId = aggregateId,
                    responseAdded.ResponseId,
                    responseAdded.ParticipantName,
                    responseAdded.SelectedOptionId,
                    responseAdded.Comment,
                    responseAdded.Timestamp
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
                    userConnected.ConnectionId,
                    userConnected.Name,
                    userConnected.ConnectedAt
                });
                break;

            case UserDisconnected userDisconnected:
                await _hubService.NotifyAdminsAsync("UserDisconnected", new
                {
                    AggregateId = aggregateId,
                    userDisconnected.ConnectionId,
                    userDisconnected.DisconnectedAt
                });
                break;

            case UserNameUpdated userNameUpdated:
                await _hubService.NotifyAdminsAsync("UserNameUpdated", new
                {
                    AggregateId = aggregateId,
                    userNameUpdated.ConnectionId,
                    userNameUpdated.Name,
                    userNameUpdated.UpdatedAt
                });
                break;

            // QuestionGroup events
            case QuestionGroupCreated groupCreated:
                await _hubService.NotifyAdminsAsync("QuestionGroupCreated",
                    new { AggregateId = aggregateId, groupCreated.Name });
                break;

            case QuestionGroupUpdated groupUpdated:
                await _hubService.NotifyAdminsAsync("QuestionGroupUpdated",
                    new { AggregateId = aggregateId, groupUpdated.NewName });
                break;

            case QuestionGroupDeleted groupDeleted:
                Console.WriteLine($"QuestionGroupDeleted event received for group ID: {aggregateId}");
                await _hubService.NotifyAdminsAsync("QuestionGroupDeleted", new { AggregateId = aggregateId });

                // 削除通知を2回送信して確実にクライアントに到達するようにする
                await Task.Delay(500); // 少し待機して最初の通知が処理される時間を確保
                Console.WriteLine($"Sending second notification for QuestionGroupDeleted: {aggregateId}");
                await _hubService.NotifyAdminsAsync("QuestionGroupDeleted",
                    new { AggregateId = aggregateId, Timestamp = DateTime.UtcNow.Ticks });
                break;

            case QuestionAddedToGroup questionAdded:
                await _hubService.NotifyAdminsAsync("QuestionAddedToGroup",
                    new { AggregateId = aggregateId, questionAdded.QuestionId, questionAdded.Order });
                break;

            case QuestionRemovedFromGroup questionRemoved:
                await _hubService.NotifyAdminsAsync("QuestionRemovedFromGroup",
                    new { AggregateId = aggregateId, questionRemoved.QuestionId });
                break;

            case QuestionOrderChanged orderChanged:
                await _hubService.NotifyAdminsAsync("QuestionOrderChanged",
                    new { AggregateId = aggregateId, orderChanged.QuestionId, orderChanged.NewOrder });
                break;

            case QuestionDisplayStarted displayStarted:
                await _sekibanOrleansExecutor.LoadAggregateAsync<QuestionProjector>(item.PartitionKeys)
                    .Conveyor(aggregate => aggregate.ToTypedPayload<Question>())
                    .Combine(aggregate =>
                        _sekibanOrleansExecutor
                            .LoadAggregateAsync<QuestionGroupProjector>(
                                PartitionKeys.Existing<QuestionGroupProjector>(aggregate.Payload.QuestionGroupId))
                            .Conveyor(group => group.ToTypedPayload<QuestionGroup>()))
                    .Do(async (question, group) =>
                    {
                        await _hubService.NotifyUniqueCodeGroupAsync(group.Payload.UniqueCode, "QuestionDisplayStarted",
                            new { QuestionId = question.PartitionKeys.AggregateId });
                    });
                break;

            case QuestionDisplayStopped displayStopped:
                await _sekibanOrleansExecutor.LoadAggregateAsync<QuestionProjector>(item.PartitionKeys)
                    .Conveyor(aggregate => aggregate.ToTypedPayload<Question>())
                    .Combine(questionPayload =>
                        _sekibanOrleansExecutor
                            .LoadAggregateAsync<QuestionGroupProjector>(
                                PartitionKeys.Existing<QuestionGroupProjector>(questionPayload.Payload.QuestionGroupId))
                            .Conveyor(groupAggregate => groupAggregate.ToTypedPayload<QuestionGroup>())
                    )
                    .Do(async (question, group) =>
                    {
                        await _hubService.NotifyUniqueCodeGroupAsync(
                            group.Payload.UniqueCode,
                            "QuestionDisplayStopped",
                            new { QuestionId = question.PartitionKeys.AggregateId }
                        );
                    });
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
                Type = ex.GetType().Name, ex.Message
            });
            await Task.CompletedTask;
        });

        // Wait until the service is cancelled
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscriptionHandle != null) await _subscriptionHandle.UnsubscribeAsync();
        await base.StopAsync(cancellationToken);
    }
}