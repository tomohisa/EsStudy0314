using ResultBoxes;
using Sekiban.Pure.Aggregates;
using Sekiban.Pure.Command.Executor;
using Sekiban.Pure.Command.Handlers;
using Sekiban.Pure.Documents;
using Sekiban.Pure.Events;

namespace MessageEachOther.Domain;

[GenerateSerializer]
public record UpdateWeatherForecastDateCommand(
    Guid WeatherForecastId,
    DateOnly NewDate
) : ICommandWithHandler<UpdateWeatherForecastDateCommand, WeatherForecastProjector>
{
    public PartitionKeys SpecifyPartitionKeys(UpdateWeatherForecastDateCommand command) => 
        PartitionKeys.Existing<WeatherForecastProjector>(command.WeatherForecastId);

    public ResultBox<EventOrNone> Handle(UpdateWeatherForecastDateCommand command, ICommandContext<IAggregatePayload> context)
        => EventOrNone.Event(new WeatherForecastDateUpdated(command.NewDate));    
}
