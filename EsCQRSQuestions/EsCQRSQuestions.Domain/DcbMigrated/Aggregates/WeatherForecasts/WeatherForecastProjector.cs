using EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Events;
using Sekiban.Dcb.Events;
using Sekiban.Dcb.Tags;

namespace EsCQRSQuestions.Domain;

public class WeatherForecastProjector : ITagProjector<WeatherForecastProjector>
{
    public static string ProjectorVersion => "1.0.0";
    public static string ProjectorName => nameof(WeatherForecastProjector);

    public static ITagStatePayload Project(ITagStatePayload current, Event ev) =>
        (current, ev.Payload) switch
        {
            (EmptyTagStatePayload, WeatherForecastInputted inputted) => new WeatherForecast(inputted.WeatherForecastId,
                inputted.Location, inputted.Date, inputted.TemperatureC, inputted.Summary),
            (WeatherForecast forecast, WeatherForecastLocationUpdated updated) => forecast with
                { Location = updated.NewLocation },
            (WeatherForecast forecast, WeatherForecastDeleted) => new DeletedWeatherForecast(forecast.Location,
                forecast.Date, forecast.TemperatureC, forecast.Summary),
            _ => current
        };
}
