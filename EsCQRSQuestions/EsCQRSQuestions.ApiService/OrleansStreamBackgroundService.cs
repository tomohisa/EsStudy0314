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
    private IAsyncStream<SerializableEvent>? _stream;
    private StreamSubscriptionHandle<SerializableEvent>? _subscriptionHandle;

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

    public async Task OnNextAsync(SerializableEvent item, StreamSequenceToken? token)
    {
        var questionId = GetQuestionId(item);
        var groupId = GetGroupId(item);
        var eventName = item.EventPayloadName ?? string.Empty;

        if (IsEventType(eventName, nameof(QuestionCreated)))
        {
            await _hubService.NotifyAdminsAsync("QuestionCreated", new { AggregateId = questionId });
            return;
        }

        if (IsEventType(eventName, nameof(QuestionUpdated)))
        {
            await _hubService.NotifyAdminsAsync("QuestionUpdated", new { AggregateId = questionId });
            return;
        }

        if (IsEventType(eventName, nameof(ResponseAdded)))
        {
            await _hubService.NotifyAdminsAsync("ResponseAdded", new { AggregateId = questionId });
            return;
        }

        if (IsEventType(eventName, nameof(QuestionDeleted)))
        {
            await _hubService.NotifyAdminsAsync("QuestionDeleted", new { AggregateId = questionId });
            return;
        }

        if (IsEventType(eventName, nameof(ActiveUsersCreated)))
        {
            await _hubService.NotifyAdminsAsync("ActiveUsersCreated", new { AggregateId = item.Id });
            return;
        }

        if (IsEventType(eventName, nameof(UserConnected)))
        {
            await _hubService.NotifyAdminsAsync("UserConnected", new { AggregateId = item.Id });
            return;
        }

        if (IsEventType(eventName, nameof(UserDisconnected)))
        {
            await _hubService.NotifyAdminsAsync("UserDisconnected", new { AggregateId = item.Id });
            return;
        }

        if (IsEventType(eventName, nameof(UserNameUpdated)))
        {
            await _hubService.NotifyAdminsAsync("UserNameUpdated", new { AggregateId = item.Id });
            return;
        }

        if (IsEventType(eventName, nameof(QuestionGroupCreated)))
        {
            await _hubService.NotifyAdminsAsync("QuestionGroupCreated", new { AggregateId = groupId });
            return;
        }

        if (IsEventType(eventName, nameof(QuestionGroupUpdated)))
        {
            await _hubService.NotifyAdminsAsync("QuestionGroupUpdated", new { AggregateId = groupId });
            return;
        }

        if (IsEventType(eventName, nameof(QuestionGroupDeleted)))
        {
            await _hubService.NotifyAdminsAsync("QuestionGroupDeleted", new { AggregateId = groupId });
            await Task.Delay(500);
            await _hubService.NotifyAdminsAsync("QuestionGroupDeleted",
                new { AggregateId = groupId, Timestamp = DateTime.UtcNow.Ticks });
            return;
        }

        if (IsEventType(eventName, nameof(QuestionAddedToGroup)))
        {
            await _hubService.NotifyAdminsAsync("QuestionAddedToGroup",
                new { AggregateId = groupId, QuestionId = questionId });
            return;
        }

        if (IsEventType(eventName, nameof(QuestionRemovedFromGroup)))
        {
            await _hubService.NotifyAdminsAsync("QuestionRemovedFromGroup",
                new { AggregateId = groupId, QuestionId = questionId });
            return;
        }

        if (IsEventType(eventName, nameof(QuestionOrderChanged)))
        {
            await _hubService.NotifyAdminsAsync("QuestionOrderChanged",
                new { AggregateId = groupId, QuestionId = questionId });
            return;
        }

        if (IsEventType(eventName, nameof(QuestionDisplayStarted)))
        {
            await NotifyDisplayEventAsync(questionId, "QuestionDisplayStarted");
            return;
        }

        if (IsEventType(eventName, nameof(QuestionDisplayStopped)))
        {
            await NotifyDisplayEventAsync(questionId, "QuestionDisplayStopped");
            return;
        }

        Console.WriteLine($"Received event: {eventName} questionId={questionId} groupId={groupId}");
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

    private Guid GetQuestionId(SerializableEvent item)
    {
        return item.Tags
            .Select(tag => _domainTypes.TagTypes.GetTag(tag))
            .OfType<QuestionTag>()
            .Select(tag => tag.QuestionId)
            .FirstOrDefault();
    }

    private Guid GetGroupId(SerializableEvent item)
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
        _stream = streamProvider.GetStream<SerializableEvent>(StreamId.Create("AllEvents", Guid.Empty));

        _subscriptionHandle = await _stream.SubscribeAsync(OnNextAsync, async ex =>
        {
            await _hubService.NotifyAdminsAsync("Error", new { Type = ex.GetType().Name, ex.Message });
            await Task.CompletedTask;
        });

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private static bool IsEventType(string actualName, string expectedSimpleName) =>
        actualName.Equals(expectedSimpleName, StringComparison.Ordinal) ||
        actualName.EndsWith("." + expectedSimpleName, StringComparison.Ordinal);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscriptionHandle != null)
        {
            await _subscriptionHandle.UnsubscribeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
