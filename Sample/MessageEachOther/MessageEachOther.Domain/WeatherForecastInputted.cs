using Orleans;
using Sekiban.Pure.Events;

namespace MessageEachOther.Domain;

[GenerateSerializer]
public record WeatherForecastInputted(
    string Location,
    DateOnly Date,
    int TemperatureC,
    string Summary
) : IEventPayload;