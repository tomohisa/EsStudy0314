using Microsoft.AspNetCore.SignalR.Client;

namespace EsCQRSQuestions.Web;

public class DefaultHttpMessageHandlerFactory : IHttpMessageHandlerFactory
{
    public HttpMessageHandler CreateHandler()
    {
        return new HttpClientHandler();
    }
}

public static class HubConnectionExtensions
{
    public static IHubConnectionBuilder WithUrlWithClientFactory(this IHubConnectionBuilder builder, string url, IHttpMessageHandlerFactory clientFactory)
    {
        return builder.WithUrl(url, options =>
        {
            options.HttpMessageHandlerFactory = _ => clientFactory.CreateHandler();
        });
    }
}
