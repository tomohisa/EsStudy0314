using Microsoft.AspNetCore.SignalR;
using Orleans.Streams;
using Sekiban.Pure.Events;
using System.Collections.Generic;

namespace MessageEachOther.ApiService;

public class OrleansStreamBackgroundService : BackgroundService
{
    private readonly IClusterClient _orleansClient;
    private readonly IEnumerable<IHubNotificationService> _hubServices;
    private Orleans.Streams.IAsyncStream<IEvent> _stream;
    private Orleans.Streams.StreamSubscriptionHandle<IEvent> _subscriptionHandle;

    public OrleansStreamBackgroundService(
        IClusterClient orleansClient,
        IEnumerable<IHubNotificationService> hubServices)
    {
        _orleansClient = orleansClient;
        _hubServices = hubServices;
    }

    public async Task OnNextAsync(IEvent item, StreamSequenceToken? token)
    {
        foreach (var hubService in _hubServices)
        {
            await hubService.NotifyAllClientsAsync("ReceiveMessage", 
                item.GetPayload().GetType().Name, 
                item.PartitionKeys.AggregateId.ToString());
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
            foreach (var hubService in _hubServices)
            {
                await hubService.NotifyAllClientsAsync("ReceiveMessage", 
                    ex.GetType().Name, 
                    ex.Message);
            }
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
