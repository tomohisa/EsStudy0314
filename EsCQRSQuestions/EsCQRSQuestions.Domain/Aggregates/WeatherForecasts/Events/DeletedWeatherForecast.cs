using EsCQRSQuestions.Domain.ValueObjects;
using Sekiban.Pure.Aggregates;

namespace EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Events;

[GenerateSerializer]
public record DeletedWeatherForecast(
    string Location,
    DateOnly Date,
    TemperatureCelsius TemperatureC,
    string Summary
) : IAggregatePayload;
