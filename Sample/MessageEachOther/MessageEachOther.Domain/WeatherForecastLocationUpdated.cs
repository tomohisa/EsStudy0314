using Sekiban.Pure.Events;

namespace MessageEachOther.Domain;

[GenerateSerializer]
public record WeatherForecastLocationUpdated(string NewLocation) : IEventPayload;
