namespace MessageEachOther.ApiService;

interface INotificationClient
{
    Task ReceiveMessage(string request);
}
