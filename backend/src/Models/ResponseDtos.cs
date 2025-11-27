// Models/ResponseDtos.cs -> mapping JSON payloads to C# DTOs
// this is the response data
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
