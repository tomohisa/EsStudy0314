using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Sekiban.Pure.Orleans;

namespace MessageEachOther.ApiService;

// [GenerateSerializer]
// public record SendMessageRequest(string ChatName, string SenderName, string Message);
//
// public class NotificationGrain : Grain, INotificationGrain
// {
//     // For SignalR.Orleans (7.2.0)
//     // ※ コホスト時は IHubContext<MyHub> でもよいですが、
//     //    Orleans 環境では SignalR.Orleans.Core.HubContext<T> を利用することで
//     //    Orleans バックプレーン経由の送信が可能になります。
//     // private readonly SignalR.Orleans.Core.HubContext<NotificationHub> _hubContext;
//     // public NotificationGrain(SignalR.Orleans.Core.HubContext<NotificationHub> hubContext)
//     // {
//     //     _hubContext = hubContext;
//     // }
//     private readonly IHubContextProvider hubContextProvider;
//
//     public NotificationGrain(IHubContextProvider hubContextProvider)
//     {
//         this.hubContextProvider = hubContextProvider;
//     }
//
//     public async Task NotifyClients(string message)
//     {
//         // ここでは、SignalR のクライアント側メソッド "ReceiveNotification" を呼び出すための
//         // InvocationMessage を作成して送信しています。
//         // ※ 下記コードは SignalR.Orleans の API によるサンプルとなります。
//         // var invocation = new InvocationMessage("ReceiveNotification", new object[] { message });
//
//         await hubContextProvider
//             .GetHubContext<NotificationHub,INotificationClient>()
//             .Clients.All.ReceiveMessage(message);
//         
//         // for SignalR.Orleans (7.2.0)
//         // 例として、グループ "AllClients" に対して通知を送信
//         // await _hubContext.Group("AllClients").Send(invocation);
//
//         // ※ 他にも、特定の接続IDへ送信する場合は
//         // await _hubContext.Client("接続ID").Send(invocation);
//     }
// }