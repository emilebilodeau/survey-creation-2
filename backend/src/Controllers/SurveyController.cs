// Controllers/SurveyController.cs -> equivalent to the survey.routes.ts file
using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SurveyApi.Models;

namespace SurveyApi.Controllers;

[ApiController]
[Route("api")]
[Authorize] // protect all routes like your middleware
public class SurveyController : ControllerBase
{
    private readonly IDbConnection _connection;

    public SurveyController(IDbConnection connection)
    {
        _connection = connection;
    }

    // Helper: get userId from token (mirror req.userId)
    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                    ?? User.FindFirst("sub")
                    ?? User.FindFirst("userId");

        if (claim == null)
            throw new UnauthorizedAccessException("User ID not found in token.");

        return int.Parse(claim.Value);
    }

    // POST /api/surveys  (create survey)
    [HttpPost("surveys")]
    public async Task<IActionResult> CreateSurvey([FromBody] CreateSurveyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) ||
            request.Questions == null ||
            request.Questions.Count == 0)
        {
            return BadRequest(new { error = "Invalid survey payload" });
        }

        var userId = GetUserId();

        await _connection.OpenAsync();
        using var transaction = _connection.BeginTransaction();

        try
        {
            // 1. Insert survey
            var insertSurveySql = @"
                INSERT INTO surveys (title, created_by, created_at)
                VALUES (@Title, @CreatedBy, NOW());
                SELECT LAST_INSERT_ID();
            ";

            var surveyId = await _connection.ExecuteScalarAsync<long>(
                insertSurveySql,
                new { Title = request.Title, CreatedBy = userId },
                transaction
            );

            // 2. Insert questions
            const string insertQuestionSql = @"
                INSERT INTO questions (survey_id, type, prompt, min, max, question_order)
                VALUES (@SurveyId, @Type, @Prompt, @Min, @Max, @Order);
            ";

            for (var i = 0; i < request.Questions.Count; i++)
            {
                var q = request.Questions[i];
                int? min = null;
                int? max = null;

                if (q.Type == "linear")
                {
                    min = q.Min;
                    max = q.Max;
                }

                await _connection.ExecuteAsync(
                    insertQuestionSql,
                    new
                    {
                        SurveyId = surveyId,
                        Type = q.Type,
                        Prompt = q.Question,
                        Min = min,
                        Max = max,
                        Order = i + 1
                    },
                    transaction
                );
            }

            transaction.Commit();
            return StatusCode(201, new { message = "Survey created", surveyId });
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Console.Error.WriteLine($"Error creating survey: {ex}");
            return StatusCode(500, new { error = "Failed to create survey" });
        }
        finally
        {
            await _connection.CloseAsync();
        }
    }

    // GET /api/surveys (homepage list)
    [HttpGet("surveys")]
    public async Task<IActionResult> GetSurveys()
    {
        var userId = GetUserId();

        const string sql = @"
            SELECT id, title, created_at
            FROM surveys
            WHERE created_by = @UserId
            ORDER BY created_at DESC;
        ";

        try
        {
            var rows = await _connection.QueryAsync<SurveySummaryDto>(sql, new { UserId = userId });
            return Ok(rows);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching surveys: {ex}");
            return StatusCode(500, new { error = "Failed to fetch surveys" });
        }
    }

    // GET /api/surveys/:id (single survey)
    [HttpGet("surveys/{id:int}")]
    public async Task<IActionResult> GetSurvey(int id)
    {
        const string sql = @"
            SELECT id, title, created_at
            FROM surveys
            WHERE id = @Id;
        ";

        try
        {
            var survey = await _connection.QuerySingleOrDefaultAsync<SurveyDto>(sql, new { Id = id });

            if (survey == null)
                return NotFound(new { error = "Survey not found" });

            return Ok(survey);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error retrieving survey: {ex}");
            return StatusCode(500, new { error = "Failed to retrieve survey" });
        }
    }

    // DELETE /api/surveys/:id
    [HttpDelete("surveys/{id:int}")]
    public async Task<IActionResult> DeleteSurvey(int id)
    {
        const string sql = "DELETE FROM surveys WHERE id = @Id;";

        try
        {
            var affected = await _connection.ExecuteAsync(sql, new { Id = id });

            if (affected == 0)
                return NotFound(new { error = "Survey not found" });

            return Ok(new { message = "Survey deleted successfully" });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error deleting survey: {ex}");
            return StatusCode(500, new { error = "Failed to delete survey" });
        }
    }

    // GET /api/surveys/:id/questions
    [HttpGet("surveys/{id:int}/questions")]
    public async Task<IActionResult> GetSurveyQuestions(int id)
    {
        const string sql = @"
            SELECT id, type, prompt AS question, min, max
            FROM questions
            WHERE survey_id = @SurveyId
            ORDER BY question_order ASC;
        ";

        try
        {
            var questions = await _connection.QueryAsync(sql, new { SurveyId = id });
            return Ok(questions);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching questions: {ex}");
            return StatusCode(500, new { error = "Failed to fetch survey questions" });
        }
    }

    // POST /api/responses (submit form)
    [HttpPost("responses")]
    public async Task<IActionResult> SubmitResponse([FromBody] SubmitResponseRequest request)
    {
        if (request.SurveyId == 0 || request.Answers == null || request.Answers.Count == 0)
        {
            return BadRequest(new { error = "Invalid response payload" });
        }

        await _connection.OpenAsync();
        using var transaction = _connection.BeginTransaction();

        try
        {
            // 1. Insert into responses
            const string insertResponseSql = @"
                INSERT INTO responses (survey_id, responded_at)
                VALUES (@SurveyId, NOW());
                SELECT LAST_INSERT_ID();
            ";

            var responseId = await _connection.ExecuteScalarAsync<long>(
                insertResponseSql,
                new { SurveyId = request.SurveyId },
                transaction
            );

            // 2. Insert answers
            const string insertAnswerSql = @"
                INSERT INTO answers (response_id, question_id, answer_text, created_at)
                VALUES (@ResponseId, @QuestionId, @AnswerText, NOW());
            ";

            foreach (var ans in request.Answers)
            {
                await _connection.ExecuteAsync(
                    insertAnswerSql,
                    new
                    {
                        ResponseId = responseId,
                        QuestionId = ans.QuestionId,
                        AnswerText = ans.Answer
                    },
                    transaction
                );
            }

            transaction.Commit();
            return StatusCode(201, new { message = "Response saved", response_id = responseId });
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Console.Error.WriteLine($"Error saving survey response: {ex}");
            return StatusCode(500, new { error = "Failed to save survey response" });
        }
        finally
        {
            await _connection.CloseAsync();
        }
    }

    // GET /api/surveys/:id/responses (data table)
    [HttpGet("surveys/{id:int}/responses")]
    public async Task<IActionResult> GetSurveyResponses(int id)
    {
        try
        {
            // 1. Questions
            const string questionsSql = @"
                SELECT id, prompt
                FROM questions
                WHERE survey_id = @SurveyId
                ORDER BY question_order;
            ";

            var questions = (await _connection.QueryAsync<(int Id, string Prompt)>(
                questionsSql,
                new { SurveyId = id }
            )).ToList();

            // 2. Responses + answers
            const string responsesSql = @"
                SELECT responses.id AS response_id,
                       responses.responded_at,
                       answers.question_id,
                       answers.answer_text
                FROM responses
                JOIN answers ON responses.id = answers.response_id
                WHERE responses.survey_id = @SurveyId
                ORDER BY responses.id, answers.question_id;
            ";

            var rows = await _connection.QueryAsync(responsesSql, new { SurveyId = id });

            // 3. Reshape like your grouped object
            var grouped = new Dictionary<long, Dictionary<string, object?>>();

            foreach (var row in rows)
            {
                long responseId = row.response_id;
                if (!grouped.TryGetValue(responseId, out var dict))
                {
                    dict = new Dictionary<string, object?>
                    {
                        ["response_id"] = responseId,
                        ["responded_at"] = row.responded_at
                    };
                    grouped[responseId] = dict;
                }

                int questionId = row.question_id;
                var q = questions.FirstOrDefault(q => q.Id == questionId);
                if (q.Prompt != null)
                {
                    dict[q.Prompt] = row.answer_text;
                }
            }

            return Ok(grouped.Values);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching survey responses: {ex}");
            return StatusCode(500, new { error = "Failed to load survey responses" });
        }
    }

    // DELETE /api/responses/:id
    [HttpDelete("responses/{id:int}")]
    public async Task<IActionResult> DeleteResponse(int id)
    {
        const string sql = "DELETE FROM responses WHERE id = @Id;";

        try
        {
            var affected = await _connection.ExecuteAsync(sql, new { Id = id });

            if (affected == 0)
                return NotFound(new { error = "Response not found" });

            return Ok(new { message = "Response deleted successfully" });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error deleting response: {ex}");
            return StatusCode(500, new { error = "Failed to delete response" });
        }
    }

    // GET /api/responses/:id/answers (prefill update form)
    [HttpGet("responses/{id:int}/answers")]
    public async Task<IActionResult> GetResponseAnswers(int id)
    {
        const string sql = @"
            SELECT question_id, answer_text AS answer
            FROM answers
            WHERE response_id = @ResponseId;
        ";

        try
        {
            var answers = await _connection.QueryAsync(sql, new { ResponseId = id });
            return Ok(answers);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading response answers: {ex}");
            return StatusCode(500, new { error = "Failed to fetch answers" });
        }
    }

    // PUT /api/responses/:id (delete & recreate answers)
    [HttpPut("responses/{id:int}")]
    public async Task<IActionResult> UpdateResponse(int id, [FromBody] UpdateResponseRequest request)
    {
        if (request.Answers == null || request.Answers.Count == 0)
        {
            return BadRequest(new { error = "Invalid payload" });
        }

        await _connection.OpenAsync();
        using var transaction = _connection.BeginTransaction();

        try
        {
            // 1. Delete existing answers
            const string deleteSql = "DELETE FROM answers WHERE response_id = @ResponseId;";
            await _connection.ExecuteAsync(deleteSql, new { ResponseId = id }, transaction);

            // 2. Reinsert
            const string insertSql = @"
                INSERT INTO answers (response_id, question_id, answer_text, created_at)
                VALUES (@ResponseId, @QuestionId, @AnswerText, NOW());
            ";

            foreach (var ans in request.Answers)
            {
                await _connection.ExecuteAsync(
                    insertSql,
                    new
                    {
                        ResponseId = id,
                        QuestionId = ans.QuestionId,
                        AnswerText = ans.Answer
                    },
                    transaction
                );
            }

            transaction.Commit();
            return Ok(new { message = "Response updated" });
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Console.Error.WriteLine($"Error updating response: {ex}");
            return StatusCode(500, new { error = "Failed to update response" });
        }
        finally
        {
            await _connection.CloseAsync();
        }
    }
}
