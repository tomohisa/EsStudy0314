using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Events;

[GenerateSerializer]
public record WeatherForecastDeleted() : IEventPayload;
