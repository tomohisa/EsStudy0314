using MessageEachOther.Domain;
using Microsoft.AspNetCore.SignalR.Client;

namespace MessageEachOther.Web;

public class WeatherApiClient(HttpClient httpClient)
{
    public async Task<WeatherForecastQuery.WeatherForecastRecord[]> GetWeatherAsync(int maxItems = 10, CancellationToken cancellationToken = default)
    {
        List<WeatherForecastQuery.WeatherForecastRecord>? forecasts = null;

        await foreach (var forecast in httpClient.GetFromJsonAsAsyncEnumerable<WeatherForecastQuery.WeatherForecastRecord>("/api/weatherforecast", cancellationToken))
        {
            if (forecasts?.Count >= maxItems)
            {
                break;
            }
            if (forecast is not null)
            {
                forecasts ??= [];
                forecasts.Add(forecast);
            }
        }

        return forecasts?.ToArray() ?? [];
    }

    public async Task InputWeatherAsync(InputWeatherForecastCommand command, CancellationToken cancellationToken = default)
    {
        await httpClient.PostAsJsonAsync("/api/inputweatherforecast", command, cancellationToken);
    }

    public async Task RemoveWeatherAsync(Guid weatherForecastId, CancellationToken cancellationToken = default)
    {
        var command = new RemoveWeatherForecastCommand(weatherForecastId);
        await httpClient.PostAsJsonAsync("/api/removeweatherforecast", command, cancellationToken);
    }

    public async Task UpdateLocationAsync(Guid weatherForecastId, string newLocation, CancellationToken cancellationToken = default)
    {
        var command = new UpdateWeatherForecastLocationCommand(weatherForecastId, newLocation);
        await httpClient.PostAsJsonAsync("/api/updateweatherforecastlocation", command, cancellationToken);
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