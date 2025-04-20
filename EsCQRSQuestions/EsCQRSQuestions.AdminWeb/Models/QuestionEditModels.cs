namespace EsCQRSQuestions.AdminWeb.Models
{
    // Shared model for the Question Form
    public class QuestionEditModel
    {
        public string Text { get; set; } = "";
        public List<QuestionOptionEditModel> Options { get; set; } = new();
        public Guid QuestionGroupId { get; set; } = Guid.Empty;
        public bool AllowMultipleResponses { get; set; } = false; // 追加：複数回答を許可するかどうか
        public string? TextError { get; set; }
        public string? OptionsError { get; set; }
    }

    // Shared model for Question Options within the form
    public class QuestionOptionEditModel
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
    }

    // Shared model for the Group Form
    public class GroupEditModel
    {
        public string Name { get; set; } = "";
        public string? NameError { get; set; }
    }
}
