using System.Text.Json.Serialization;
using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events;
using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Payloads;
using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Events;
using Sekiban.Pure.Events;

namespace EsCQRSQuestions.Domain;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(EventDocumentCommon))]
[JsonSerializable(typeof(EventDocumentCommon[]))]
// Weather Forecast Events
[JsonSerializable(typeof(EventDocument<WeatherForecastInputted>))]
[JsonSerializable(typeof(WeatherForecastInputted))]
[JsonSerializable(typeof(EventDocument<WeatherForecastDeleted>))]
[JsonSerializable(typeof(WeatherForecastDeleted))]
[JsonSerializable(typeof(EventDocument<WeatherForecastLocationUpdated>))]
[JsonSerializable(typeof(WeatherForecastLocationUpdated))]

// Question Events
[JsonSerializable(typeof(EventDocument<QuestionCreated>))]
[JsonSerializable(typeof(QuestionCreated))]
[JsonSerializable(typeof(EventDocument<QuestionUpdated>))]
[JsonSerializable(typeof(QuestionUpdated))]
[JsonSerializable(typeof(EventDocument<QuestionDisplayStarted>))]
[JsonSerializable(typeof(QuestionDisplayStarted))]
[JsonSerializable(typeof(EventDocument<QuestionDisplayStopped>))]
[JsonSerializable(typeof(QuestionDisplayStopped))]
[JsonSerializable(typeof(EventDocument<ResponseAdded>))]
[JsonSerializable(typeof(ResponseAdded))]
[JsonSerializable(typeof(EventDocument<QuestionDeleted>))]
[JsonSerializable(typeof(QuestionDeleted))]

// Question Payload Types
[JsonSerializable(typeof(QuestionOption))]
[JsonSerializable(typeof(QuestionResponse))]

// ActiveUsers Events
[JsonSerializable(typeof(EventDocument<ActiveUsersCreated>))]
[JsonSerializable(typeof(ActiveUsersCreated))]
[JsonSerializable(typeof(EventDocument<UserConnected>))]
[JsonSerializable(typeof(UserConnected))]
[JsonSerializable(typeof(EventDocument<UserDisconnected>))]
[JsonSerializable(typeof(UserDisconnected))]
[JsonSerializable(typeof(EventDocument<UserNameUpdated>))]
[JsonSerializable(typeof(UserNameUpdated))]

// ActiveUsers Payload Types
[JsonSerializable(typeof(ActiveUsersAggregate))]
[JsonSerializable(typeof(ActiveUser))]
public partial class EsCQRSQuestionsDomainEventsJsonContext : JsonSerializerContext
{
}
