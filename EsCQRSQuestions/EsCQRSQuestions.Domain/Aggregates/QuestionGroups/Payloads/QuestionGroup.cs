using Sekiban.Pure.Aggregates;

namespace EsCQRSQuestions.Domain.Aggregates.QuestionGroups.Payloads
{
    /// <summary>
    /// Represents a reference to a question within a group, including its order.
    /// </summary>
    [GenerateSerializer]
    public record QuestionReference(Guid QuestionId, int Order);

    /// <summary>
    /// Represents the state of a Question Group aggregate.
    /// </summary>
    [GenerateSerializer, Immutable]
    public record QuestionGroup(
        string Name,
        string UniqueCode,  // 新規追加：6桁の英数字
        List<QuestionReference> Questions) : IAggregatePayload
    {
        public QuestionGroup() : this("", "", new List<QuestionReference>()) { }

        /// <summary>
        /// Adds a question to the group, ensuring no duplicates and assigning the next order.
        /// </summary>
        public QuestionGroup AddQuestion(Guid questionId)
        {
            if (Questions.Any(q => q.QuestionId == questionId))
            {
                return this; // Question already exists
            }
            var nextOrder = Questions.Count > 0 ? Questions.Max(q => q.Order) + 1 : 0;
            var updatedQuestions = Questions.Append(new QuestionReference(questionId, nextOrder)).ToList();
            return this with { Questions = updatedQuestions };
        }

        /// <summary>
        /// Removes a question from the group and reorders the remaining questions.
        /// </summary>
        public QuestionGroup RemoveQuestion(Guid questionId)
        {
            var questionToRemove = Questions.FirstOrDefault(q => q.QuestionId == questionId);
            if (questionToRemove == null)
            {
                return this; // Question not found
            }

            var updatedQuestions = Questions
                .Where(q => q.QuestionId != questionId)
                .OrderBy(q => q.Order)
                .Select((q, index) => q with { Order = index }) // Re-assign order sequentially
                .ToList();

            return this with { Questions = updatedQuestions };
        }

        /// <summary>
        /// Changes the order of a specific question within the group.
        /// </summary>
        public QuestionGroup ChangeQuestionOrder(Guid questionId, int newOrder)
        {
            var questionToMove = Questions.FirstOrDefault(q => q.QuestionId == questionId);
            if (questionToMove == null || newOrder < 0 || newOrder >= Questions.Count)
            {
                // Invalid operation: question not found or new order out of bounds
                return this;
            }

            var currentOrder = questionToMove.Order;
            if (currentOrder == newOrder)
            {
                return this; // No change needed
            }

            var otherQuestions = Questions.Where(q => q.QuestionId != questionId).ToList();
            var updatedQuestions = new List<QuestionReference>();

            // Insert the moved question at the new position
            otherQuestions.Insert(newOrder, questionToMove);

            // Re-assign order based on the new list sequence
            for (int i = 0; i < otherQuestions.Count; i++)
            {
                updatedQuestions.Add(otherQuestions[i] with { Order = i });
            }

            return this with { Questions = updatedQuestions };
        }

        /// <summary>
        /// Updates the name of the group.
        /// </summary>
        public QuestionGroup UpdateName(string newName)
        {
            return this with { Name = newName };
        }

        /// <summary>
        /// Updates the unique code of the group.
        /// </summary>
        public QuestionGroup UpdateUniqueCode(string newUniqueCode)
        {
            return this with { UniqueCode = newUniqueCode };
        }
    }
}
