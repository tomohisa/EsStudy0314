using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;
using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Queries;
using EsCQRSQuestions.Domain.DcbTags;
using Orleans.Streams;
using Sekiban.Dcb;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.ApiService;

public class OrleansStreamBackgroundService : BackgroundService
{
    private readonly IClusterClient _orleansClient;
    private readonly IHubNotificationService _hubService;
    private readonly ISekibanExecutor _executor;
    private readonly DcbDomainTypes _domainTypes;
    private IAsyncStream<Event>? _stream;
    private StreamSubscriptionHandle<Event>? _subscriptionHandle;

    public OrleansStreamBackgroundService(
        IClusterClient orleansClient,
        IHubNotificationService hubService,
        ISekibanExecutor executor,
        DcbDomainTypes domainTypes)
    {
        _orleansClient = orleansClient;
        _hubService = hubService;
        _executor = executor;
        _domainTypes = domainTypes;
    }

    public async Task OnNextAsync(Event item, StreamSequenceToken? token)
    {
        var questionId = GetQuestionId(item);
        var groupId = GetGroupId(item);

        switch (item.Payload)
        {
            case QuestionCreated:
                await _hubService.NotifyAdminsAsync("QuestionCreated", new { AggregateId = questionId });
                break;

            case QuestionUpdated:
                await _hubService.NotifyAdminsAsync("QuestionUpdated", new { AggregateId = questionId });
                break;

            case ResponseAdded responseAdded:
                await _hubService.NotifyAdminsAsync("ResponseAdded", new
                {
                    AggregateId = questionId,
                    responseAdded.ResponseId,
                    responseAdded.ParticipantName,
                    responseAdded.SelectedOptionId,
                    responseAdded.Comment,
                    responseAdded.Timestamp
                });
                break;

            case QuestionDeleted:
                await _hubService.NotifyAdminsAsync("QuestionDeleted", new { AggregateId = questionId });
                break;

            case ActiveUsersCreated:
                await _hubService.NotifyAdminsAsync("ActiveUsersCreated", new { AggregateId = item.Id });
                break;

            case UserConnected userConnected:
                await _hubService.NotifyAdminsAsync("UserConnected", new
                {
                    AggregateId = item.Id,
                    userConnected.ConnectionId,
                    userConnected.Name,
                    userConnected.ConnectedAt
                });
                break;

            case UserDisconnected userDisconnected:
                await _hubService.NotifyAdminsAsync("UserDisconnected", new
                {
                    AggregateId = item.Id,
                    userDisconnected.ConnectionId,
                    userDisconnected.DisconnectedAt
                });
                break;

            case UserNameUpdated userNameUpdated:
                await _hubService.NotifyAdminsAsync("UserNameUpdated", new
                {
                    AggregateId = item.Id,
                    userNameUpdated.ConnectionId,
                    userNameUpdated.Name,
                    userNameUpdated.UpdatedAt
                });
                break;

            case QuestionGroupCreated groupCreated:
                await _hubService.NotifyAdminsAsync("QuestionGroupCreated", new
                    { AggregateId = groupCreated.GroupId, groupCreated.Name });
                break;

            case QuestionGroupUpdated groupUpdated:
                await _hubService.NotifyAdminsAsync("QuestionGroupUpdated", new
                    { AggregateId = groupUpdated.GroupId, groupUpdated.NewName });
                break;

            case QuestionGroupDeleted groupDeleted:
                await _hubService.NotifyAdminsAsync("QuestionGroupDeleted", new { AggregateId = groupDeleted.GroupId });
                await Task.Delay(500);
                await _hubService.NotifyAdminsAsync("QuestionGroupDeleted",
                    new { AggregateId = groupDeleted.GroupId, Timestamp = DateTime.UtcNow.Ticks });
                break;

            case QuestionAddedToGroup questionAdded:
                await _hubService.NotifyAdminsAsync("QuestionAddedToGroup",
                    new { AggregateId = questionAdded.GroupId, questionAdded.QuestionId, questionAdded.Order });
                break;

            case QuestionRemovedFromGroup questionRemoved:
                await _hubService.NotifyAdminsAsync("QuestionRemovedFromGroup",
                    new { AggregateId = questionRemoved.GroupId, questionRemoved.QuestionId });
                break;

            case QuestionOrderChanged orderChanged:
                await _hubService.NotifyAdminsAsync("QuestionOrderChanged",
                    new { AggregateId = orderChanged.GroupId, orderChanged.QuestionId, orderChanged.NewOrder });
                break;

            case QuestionDisplayStarted:
                await NotifyDisplayEventAsync(questionId, "QuestionDisplayStarted");
                break;

            case QuestionDisplayStopped:
                await NotifyDisplayEventAsync(questionId, "QuestionDisplayStopped");
                break;

            default:
                Console.WriteLine($"Received event: {item.Payload.GetType().Name} questionId={questionId} groupId={groupId}");
                break;
        }
    }

    private async Task NotifyDisplayEventAsync(Guid questionId, string eventName)
    {
        if (questionId == Guid.Empty)
        {
            return;
        }

        var questionResult = await _executor.QueryAsync(new QuestionDetailQuery(questionId));
        if (!questionResult.IsSuccess)
        {
            return;
        }

        var groupId = questionResult.GetValue().QuestionGroupId;
        if (groupId == Guid.Empty)
        {
            return;
        }

        var groupResult = await _executor.QueryAsync(new GetQuestionGroupByGroupIdQuery(groupId));
        if (!groupResult.IsSuccess)
        {
            return;
        }

        await _hubService.NotifyUniqueCodeGroupAsync(groupResult.GetValue().UniqueCode, eventName, new { QuestionId = questionId });
    }

    private Guid GetQuestionId(Event item)
    {
        return item.Tags
            .Select(tag => _domainTypes.TagTypes.GetTag(tag))
            .OfType<QuestionTag>()
            .Select(tag => tag.QuestionId)
            .FirstOrDefault();
    }

    private Guid GetGroupId(Event item)
    {
        return item.Tags
            .Select(tag => _domainTypes.TagTypes.GetTag(tag))
            .OfType<QuestionGroupTag>()
            .Select(tag => tag.GroupId)
            .FirstOrDefault();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var streamProvider = _orleansClient.GetStreamProvider("EventStreamProvider");
        _stream = streamProvider.GetStream<Event>(StreamId.Create("AllEvents", Guid.Empty));

        _subscriptionHandle = await _stream.SubscribeAsync(OnNextAsync, async ex =>
        {
            await _hubService.NotifyAdminsAsync("Error", new { Type = ex.GetType().Name, ex.Message });
            await Task.CompletedTask;
        });

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
