// Models/ResponseDtos.cs -> mapping JSON payloads to C# DTOs
// models for response submissions/updates
// this is pure input binding, AnswerRequest is reused in both inputs
using System.Text.Json.Serialization;

namespace SurveyApi.Models;

public class SubmitResponseRequest
{
    [JsonPropertyName("survey_id")]
    public int SurveyId { get; set; }

    [JsonPropertyName("answers")]
    public List<AnswerRequest> Answers { get; set; } = new();
}

public class AnswerRequest
{
    [JsonPropertyName("question_id")]
    public int QuestionId { get; set; }

    [JsonPropertyName("answer")]
    public string Answer { get; set; } = default!;
}

public class UpdateResponseRequest
{
    [JsonPropertyName("answers")]
    public List<AnswerRequest> Answers { get; set; } = new();
}
