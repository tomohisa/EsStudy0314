using EsCQRSQuestions.Domain.DcbTags;
using EsCQRSQuestions.Domain.ValueObjects;
using ResultBoxes;
using Sekiban.Dcb.MultiProjections;
using Sekiban.Dcb.Queries;

namespace EsCQRSQuestions.Domain;

[GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
public record WeatherForecastQuery(string LocationContains)
    : IMultiProjectionListQuery<GenericTagMultiProjector<WeatherForecastProjector, WeatherForecastTag>,
        WeatherForecastQuery,
        WeatherForecastQuery.WeatherForecastRecord>
{
    public int? PageSize { get; init; }
    public int? PageNumber { get; init; }

    public static ResultBox<IEnumerable<WeatherForecastRecord>> HandleFilter(
        GenericTagMultiProjector<WeatherForecastProjector, WeatherForecastTag> projection,
        WeatherForecastQuery query,
        IQueryContext context)
    {
        return projection.GetCurrentTagStates().Values
            .Where(m => m.Payload is WeatherForecast)
            .Select(m => (WeatherForecast)m.Payload)
            .Where(item => string.IsNullOrEmpty(query.LocationContains) ||
                           item.Location.Contains(query.LocationContains, StringComparison.OrdinalIgnoreCase))
            .Select(item => new WeatherForecastRecord(item.WeatherForecastId, item.Location, item.Date, item.TemperatureC,
                item.Summary, item.TemperatureC.GetFahrenheit()))
            .ToResultBox();
    }

    public static ResultBox<IEnumerable<WeatherForecastRecord>> HandleSort(
        IEnumerable<WeatherForecastRecord> filteredList,
        WeatherForecastQuery query,
        IQueryContext context) => filteredList.OrderBy(m => m.Date).AsEnumerable().ToResultBox();

    [GenerateSerializer]
    public record WeatherForecastRecord(
        Guid WeatherForecastId,
        string Location,
        DateOnly Date,
        TemperatureCelsius TemperatureC,
        string Summary,
        double TemperatureF);
}
