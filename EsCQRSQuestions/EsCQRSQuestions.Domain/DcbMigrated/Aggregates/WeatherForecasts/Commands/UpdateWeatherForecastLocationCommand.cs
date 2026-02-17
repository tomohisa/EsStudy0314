using EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Events;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.Commands;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Commands;

[GenerateSerializer]
public record UpdateWeatherForecastLocationCommand(Guid WeatherForecastId, string NewLocation)
    : ICommandWithHandler<UpdateWeatherForecastLocationCommand>
{
    public static Task<ResultBox<EventOrNone>> HandleAsync(UpdateWeatherForecastLocationCommand command,
        ICommandContext context) =>
        Task.FromResult(EventOrNone.EventWithTags(new WeatherForecastLocationUpdated(command.NewLocation),
            new WeatherForecastTag(command.WeatherForecastId)));
}
