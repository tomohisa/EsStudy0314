using EsCQRSQuestions.Domain.ValueObjects;
using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Events;

[GenerateSerializer]
public record WeatherForecastInputted(
    string Location,
    DateOnly Date,
    TemperatureCelsius TemperatureC,
    string Summary
) : IEventPayload;
