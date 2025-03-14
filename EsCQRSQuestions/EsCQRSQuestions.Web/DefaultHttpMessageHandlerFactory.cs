using Microsoft.AspNetCore.SignalR.Client;

namespace EsCQRSQuestions.Web;

// This is a custom interface to avoid conflicts with System.Net.Http.IHttpMessageHandlerFactory
public interface ICustomHttpMessageHandlerFactory
{
    HttpMessageHandler CreateHandler();
}

public class DefaultHttpMessageHandlerFactory : ICustomHttpMessageHandlerFactory
{
    public HttpMessageHandler CreateHandler()
    {
        return new HttpClientHandler();
    }
}

public static class HubConnectionExtensions
{
    public static IHubConnectionBuilder WithUrlWithClientFactory(this IHubConnectionBuilder builder, string url, ICustomHttpMessageHandlerFactory clientFactory)
    {
        return builder.WithUrl(url, options =>
        {
            options.HttpMessageHandlerFactory = _ => clientFactory.CreateHandler();
        });
    }
}
