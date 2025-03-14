using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Events;

[GenerateSerializer]
public record WeatherForecastLocationUpdated(string NewLocation) : IEventPayload;
