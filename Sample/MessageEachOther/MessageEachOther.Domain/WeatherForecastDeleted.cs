using Orleans;
using Sekiban.Pure.Events;

namespace MessageEachOther.Domain;

[GenerateSerializer]
public record WeatherForecastDeleted() : IEventPayload;
