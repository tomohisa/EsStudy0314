using EsCQRSQuestions.Domain.Aggregates.ActiveUsers;
using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Events;
using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Payloads;
using EsCQRSQuestions.Domain.Aggregates.ActiveUsers.Queries;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Events;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads;
using EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Queries;
using EsCQRSQuestions.Domain.Aggregates.Questions;
using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using EsCQRSQuestions.Domain.Aggregates.Questions.Queries;
using EsCQRSQuestions.Domain.Aggregates.WeatherForecasts.Events;
using EsCQRSQuestions.Domain.DcbTags;
using EsCQRSQuestions.Domain.Projections.Questions;
using Sekiban.Dcb;
using Sekiban.Dcb.MultiProjections;
using System.Text.Json;

namespace EsCQRSQuestions.Domain.Generated;

public static class EsCQRSQuestionsDomainDomainTypes
{
    public static DcbDomainTypes Generate(JsonSerializerOptions? options = null)
    {
        return DcbDomainTypesExtensions.Simple(types =>
        {
            types.EventTypes.RegisterEventType<QuestionCreated>();
            types.EventTypes.RegisterEventType<QuestionUpdated>();
            types.EventTypes.RegisterEventType<QuestionDeleted>();
            types.EventTypes.RegisterEventType<QuestionDisplayStarted>();
            types.EventTypes.RegisterEventType<QuestionDisplayStopped>();
            types.EventTypes.RegisterEventType<ResponseAdded>();
            types.EventTypes.RegisterEventType<QuestionGroupIdUpdated>();

            types.EventTypes.RegisterEventType<QuestionGroupCreated>();
            types.EventTypes.RegisterEventType<QuestionGroupUpdated>();
            types.EventTypes.RegisterEventType<QuestionGroupDeleted>();
            types.EventTypes.RegisterEventType<QuestionGroupNameUpdated>();
            types.EventTypes.RegisterEventType<QuestionAddedToGroup>();
            types.EventTypes.RegisterEventType<QuestionRemovedFromGroup>();
            types.EventTypes.RegisterEventType<QuestionOrderChanged>();

            types.EventTypes.RegisterEventType<ActiveUsersCreated>();
            types.EventTypes.RegisterEventType<UserConnected>();
            types.EventTypes.RegisterEventType<UserDisconnected>();
            types.EventTypes.RegisterEventType<UserNameUpdated>();

            types.EventTypes.RegisterEventType<WeatherForecastInputted>();
            types.EventTypes.RegisterEventType<WeatherForecastLocationUpdated>();
            types.EventTypes.RegisterEventType<WeatherForecastDeleted>();

            types.TagProjectorTypes.RegisterProjector<QuestionProjector>();
            types.TagProjectorTypes.RegisterProjector<QuestionGroupProjector>();
            types.TagProjectorTypes.RegisterProjector<ActiveUsersProjector>();
            types.TagProjectorTypes.RegisterProjector<WeatherForecastProjector>();

            types.TagStatePayloadTypes.RegisterPayloadType<Question>();
            types.TagStatePayloadTypes.RegisterPayloadType<DeletedQuestion>();
            types.TagStatePayloadTypes.RegisterPayloadType<QuestionGroup>();
            types.TagStatePayloadTypes.RegisterPayloadType<DeletedQuestionGroup>();
            types.TagStatePayloadTypes.RegisterPayloadType<ActiveUsersAggregate>();
            types.TagStatePayloadTypes.RegisterPayloadType<WeatherForecast>();
            types.TagStatePayloadTypes.RegisterPayloadType<DeletedWeatherForecast>();

            types.TagTypes.RegisterTagGroupType<QuestionTag>();
            types.TagTypes.RegisterTagGroupType<QuestionGroupTag>();
            types.TagTypes.RegisterTagGroupType<ActiveUsersTag>();
            types.TagTypes.RegisterTagGroupType<WeatherForecastTag>();

            types.MultiProjectorTypes.RegisterProjectorWithCustomSerialization<
                GenericTagMultiProjector<QuestionProjector, QuestionTag>>();
            types.MultiProjectorTypes.RegisterProjectorWithCustomSerialization<
                GenericTagMultiProjector<QuestionGroupProjector, QuestionGroupTag>>();
            types.MultiProjectorTypes.RegisterProjectorWithCustomSerialization<
                GenericTagMultiProjector<ActiveUsersProjector, ActiveUsersTag>>();
            types.MultiProjectorTypes.RegisterProjectorWithCustomSerialization<
                GenericTagMultiProjector<WeatherForecastProjector, WeatherForecastTag>>();
            types.MultiProjectorTypes.RegisterProjector<QuestionsMultiProjector>();

            types.QueryTypes.RegisterListQuery<WeatherForecastQuery>();
            types.QueryTypes.RegisterQuery<ActiveUsersQuery>();
            types.QueryTypes.RegisterQuery<QuestionDetailQuery>();
            types.QueryTypes.RegisterQuery<ActiveQuestionQuery>();
            types.QueryTypes.RegisterQuery<QuestionGroupExistsQuery>();
            types.QueryTypes.RegisterListQuery<QuestionListQuery>();
            types.QueryTypes.RegisterListQuery<QuestionsQuery>();
            types.QueryTypes.RegisterListQuery<GetQuestionGroupsQuery>();
            types.QueryTypes.RegisterListQuery<GetQuestionsByGroupIdQuery>();
            types.QueryTypes.RegisterQuery<GetQuestionGroupByGroupIdQuery>();
        }, options);
    }
}
