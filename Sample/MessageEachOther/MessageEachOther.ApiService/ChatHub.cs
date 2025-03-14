using Microsoft.AspNetCore.SignalR;
using Orleans.Streams;
using Sekiban.Pure.Events;
using StreamSequenceToken = Orleans.Streams.StreamSequenceToken;

namespace MessageEachOther.ApiService;

public class ChatHub(IClusterClient clusterClient) : Hub
{
    // 固定のグループ名
    private const string FixedGroupName = "FixedGroup";
    
    // クライアント接続時に自動でグループに追加
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, FixedGroupName);
        await base.OnConnectedAsync();
        
        var streamProvider = clusterClient.GetStreamProvider("EventStreamProvider");
        var stream = streamProvider.GetStream<IEvent>(StreamId.Create("AllEvents", Guid.Empty));

// イベント購読（ラムダ式を使用した例）
        await stream.SubscribeAsync((IEvent evt, StreamSequenceToken token) =>
        {
            // 受信したイベントの処理
            Console.WriteLine($"Received event: {evt}");
            return Task.CompletedTask;
        });
    }

    // クライアントから呼ばれるメッセージ送信メソッド
    public async Task SendMessage(string user, string message)
    {
        // 固定グループ内の全クライアントにメッセージを送信
        await Clients.Group(FixedGroupName).SendAsync("ReceiveMessage", user, message);
    }
}