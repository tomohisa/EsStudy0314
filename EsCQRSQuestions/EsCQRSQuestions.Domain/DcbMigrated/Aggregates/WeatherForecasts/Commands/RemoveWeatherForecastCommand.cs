using EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Events;
using EsCQRSQuestions.Domain.DcbTags;
using ResultBoxes;
using Sekiban.Dcb.Commands;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Commands;

[GenerateSerializer]
public record RemoveWeatherForecastCommand(Guid WeatherForecastId) : ICommandWithHandler<RemoveWeatherForecastCommand>
{
    public static Task<ResultBox<EventOrNone>> HandleAsync(RemoveWeatherForecastCommand command, ICommandContext context) =>
        Task.FromResult(EventOrNone.EventWithTags(new WeatherForecastDeleted(),
            new WeatherForecastTag(command.WeatherForecastId)));
}
