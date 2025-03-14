using System.Text.Json.Serialization;
using Sekiban.Pure.Events;

namespace MessageEachOther.Domain;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(EventDocumentCommon))]
[JsonSerializable(typeof(EventDocumentCommon[]))]
[JsonSerializable(typeof(EventDocument<MessageEachOther.Domain.WeatherForecastInputted>))]
[JsonSerializable(typeof(MessageEachOther.Domain.WeatherForecastInputted))]
[JsonSerializable(typeof(EventDocument<MessageEachOther.Domain.WeatherForecastDeleted>))]
[JsonSerializable(typeof(MessageEachOther.Domain.WeatherForecastDeleted))]
[JsonSerializable(typeof(EventDocument<MessageEachOther.Domain.WeatherForecastLocationUpdated>))]
[JsonSerializable(typeof(MessageEachOther.Domain.WeatherForecastLocationUpdated))]
[JsonSerializable(typeof(EventDocument<MessageEachOther.Domain.WeatherForecastDateUpdated>))]
[JsonSerializable(typeof(MessageEachOther.Domain.WeatherForecastDateUpdated))]
public partial class MessageEachOtherDomainEventsJsonContext : JsonSerializerContext
{
}
