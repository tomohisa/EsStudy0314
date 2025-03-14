using EsCQRSQuestions.Domain.ValueObjects;
using Sekiban.Pure.Aggregates;

namespace EsCQRSQuestions.Domain;

[GenerateSerializer]
public record WeatherForecast(
    string Location,
    DateOnly Date,
    TemperatureCelsius TemperatureC,
    string Summary
) : IAggregatePayload;
