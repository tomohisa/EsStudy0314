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
    [JsonSerializable(typeof(Sekiban.Pure.Aggregates.EmptyAggregatePayload))]
    [JsonSerializable(typeof(Sekiban.Pure.Projectors.IMultiProjectorCommon))]
    [JsonSerializable(typeof(Sekiban.Pure.Documents.PartitionKeys))]
    [JsonSerializable(typeof(Sekiban.Pure.Projectors.SerializableAggregateListProjector))]
    [JsonSerializable(typeof(Sekiban.Pure.Aggregates.SerializableAggregate))]
    [JsonSerializable(typeof(EventDocument<EsCQRSQuestions.Domain.Aggregates.Questions.Events.QuestionCreated>))]
    [JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.Questions.Events.QuestionCreated))]
    [JsonSerializable(typeof(EventDocument<EsCQRSQuestions.Domain.Aggregates.Questions.Events.QuestionDeleted>))]
    [JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.Questions.Events.QuestionDeleted))]
    [JsonSerializable(typeof(EventDocument<EsCQRSQuestions.Domain.Aggregates.Questions.Events.QuestionDisplayStarted>))]
    [JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.Questions.Events.QuestionDisplayStarted))]
    [JsonSerializable(typeof(EventDocument<EsCQRSQuestions.Domain.Aggregates.Questions.Events.QuestionDisplayStopped>))]
    [JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.Questions.Events.QuestionDisplayStopped))]
    [JsonSerializable(typeof(EventDocument<EsCQRSQuestions.Domain.Aggregates.Questions.Events.QuestionUpdated>))]
    [JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.Questions.Events.QuestionUpdated))]
    [JsonSerializable(typeof(EventDocument<EsCQRSQuestions.Domain.Aggregates.Questions.Events.ResponseAdded>))]
    [JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.Questions.Events.ResponseAdded))]
    [JsonSerializable(typeof(EventDocument<EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Events.WeatherForecastDeleted>))]
    [JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Events.WeatherForecastDeleted))]
    [JsonSerializable(typeof(EventDocument<EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Events.WeatherForecastInputted>))]
    [JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Events.WeatherForecastInputted))]
    [JsonSerializable(typeof(EventDocument<EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Events.WeatherForecastLocationUpdated>))]
    [JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Events.WeatherForecastLocationUpdated))]
    [JsonSerializable(typeof(EventDocument<EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events.ActiveUsersCreated>))]
    [JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events.ActiveUsersCreated))]
    [JsonSerializable(typeof(EventDocument<EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events.UserNameUpdated>))]
    [JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events.UserNameUpdated))]
    [JsonSerializable(typeof(EventDocument<EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events.UserConnected>))]
    [JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events.UserConnected))]
    [JsonSerializable(typeof(EventDocument<EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events.UserDisconnected>))]
    [JsonSerializable(typeof(EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events.UserDisconnected))]
    public partial class EsCQRSQuestionsDomainEventsJsonContext : JsonSerializerContext
    {
    }
