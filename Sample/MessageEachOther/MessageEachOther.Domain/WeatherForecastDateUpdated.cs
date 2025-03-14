using Sekiban.Pure.Events;

namespace MessageEachOther.Domain;

[GenerateSerializer]
public record WeatherForecastDateUpdated(DateOnly NewDate) : IEventPayload;
