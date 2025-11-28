// Models/SurveyDtos.cs -> mapping JSON payloads to C# DTOs
// models for creating and reading surveys
// contains both input binding and output binding
// CreateSurveyRequest, CreateQuestionRequest: input
// SuverySummaryDto & SurveyDto: output
using System.Text.Json.Serialization;

namespace SurveyApi.Models;

public class CreateSurveyRequest
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = default!;

    [JsonPropertyName("questions")]
    public List<CreateQuestionRequest> Questions { get; set; } = new();
}

public class CreateQuestionRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = default!;

    // In TS: q.question
    [JsonPropertyName("question")]
    public string Question { get; set; } = default!;

    [JsonPropertyName("min")]
    public int? Min { get; set; }

    [JsonPropertyName("max")]
    public int? Max { get; set; }
}

public class SurveySummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}

public class SurveyDto
{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}
