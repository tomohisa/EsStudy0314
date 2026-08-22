using EsCQRSQuestions.Domain.Aggregates.Questions.Events;
using EsCQRSQuestions.Domain.Aggregates.Questions.Payloads;
using Sekiban.Dcb.Events;
using Sekiban.Dcb.Tags;

namespace EsCQRSQuestions.Domain.Aggregates.Questions;

public class QuestionParticipantResponseProjector : ITagProjector<QuestionParticipantResponseProjector>
{
    public static string ProjectorVersion => "1.0.0";
    public static string ProjectorName => nameof(QuestionParticipantResponseProjector);

    public static ITagStatePayload Project(ITagStatePayload current, Event ev) =>
        (current, ev.Payload) switch
        {
            (EmptyTagStatePayload, ResponseAdded added) => new QuestionParticipantResponse(
                added.ClientId,
                added.ResponseId,
                added.SelectedOptionId,
                added.Comment,
                added.Timestamp),
            (QuestionParticipantResponse state, ResponseAdded added) => state with
            {
                LastResponseId = added.ResponseId,
                LastSelectedOptionId = added.SelectedOptionId,
                LastComment = added.Comment,
                LastTimestamp = added.Timestamp
            },
            (QuestionParticipantResponse state, ResponseCommentUpdated updated)
                when state.LastResponseId == updated.ResponseId || state.ClientId == updated.ClientId => state with
            {
                LastComment = updated.Comment,
                LastTimestamp = updated.Timestamp
            },
            _ => current
        };
}
