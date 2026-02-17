using EsCQRSQuestions.Domain.ValueObjects;
using Sekiban.Dcb.Tags;

namespace EsCQRSQuestions.Domain;

[GenerateSerializer]
public record WeatherForecast(
    Guid WeatherForecastId,
    string Location,
    DateOnly Date,
    TemperatureCelsius TemperatureC,
    string Summary) : ITagStatePayload;

[GenerateSerializer]
public record DeletedWeatherForecast(
    string Location,
    DateOnly Date,
    TemperatureCelsius TemperatureC,
    string Summary) : ITagStatePayload;
