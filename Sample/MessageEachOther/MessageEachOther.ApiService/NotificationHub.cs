using Microsoft.AspNetCore.SignalR;

namespace MessageEachOther.ApiService;

public class NotificationHub : Hub
{
    // 固定のグループ名
    private const string FixedGroupName = "AllClients";

    // クライアント接続時に自動でグループに追加
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, FixedGroupName);
        await base.OnConnectedAsync();
    }

    // クライアントから呼ばれるメッセージ送信メソッド
    public async Task SendMessage(string user, string message)
    {
        // 固定グループ内の全クライアントにメッセージを送信
        await Clients.Group(FixedGroupName).SendAsync("ReceiveMessage", user, message);
    }
}
