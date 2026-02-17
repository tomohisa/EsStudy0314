using EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Events;
using EsCQRSQuestions.Domain.DcbTags;
using EsCQRSQuestions.Domain.ValueObjects;
using ResultBoxes;
using Sekiban.Dcb.Commands;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Commands;

[GenerateSerializer]
public record InputWeatherForecastCommand(
    string Location,
    DateOnly Date,
    TemperatureCelsius TemperatureC,
    string Summary
) : ICommandWithHandler<InputWeatherForecastCommand>
{
    public static Task<ResultBox<EventOrNone>> HandleAsync(InputWeatherForecastCommand command, ICommandContext context)
    {
        var weatherForecastId = Guid.CreateVersion7();
        return Task.FromResult(EventOrNone.EventWithTags(
            new WeatherForecastInputted(weatherForecastId, command.Location, command.Date, command.TemperatureC,
                command.Summary),
            new WeatherForecastTag(weatherForecastId)));
    }
}
