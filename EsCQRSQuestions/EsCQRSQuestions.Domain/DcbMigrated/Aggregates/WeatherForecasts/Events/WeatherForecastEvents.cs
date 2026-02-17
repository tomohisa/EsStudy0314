using EsCQRSQuestions.Domain.ValueObjects;
using Sekiban.Dcb.Events;

namespace EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Events;

[GenerateSerializer]
public record WeatherForecastInputted(
    Guid WeatherForecastId,
    string Location,
    DateOnly Date,
    TemperatureCelsius TemperatureC,
    string Summary) : IEventPayload;

[GenerateSerializer]
public record WeatherForecastLocationUpdated(string NewLocation) : IEventPayload;

[GenerateSerializer]
public record WeatherForecastDeleted() : IEventPayload;
