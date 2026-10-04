using System.Security.Claims;
using System.Text.Json;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

/// <summary>
/// Student portal attempt flow: dashboard of active assignments, start/submit attempt flow
/// with server-side timer validation, immediate auto-graded feedback and XP.
/// Everything returned here is pseudonymous and answer-key-free.
/// </summary>
[ApiController]
[Route("api/student/assignments")]
[Authorize(Roles = AppRoles.User)]
public class StudentAssignmentsController(AppDbContext db) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private string StudentId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>
    /// Current (Active) and past (Closed) assignments with the caller's own attempt
    /// state per assignment. Drafts stay invisible to students. Visible assignments are
    /// General (no category) plus assignments filed into any category the caller
    /// belongs to.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<StudentAssignmentSummaryDto>>> Dashboard()
    {
        var me = StudentId;
        var rows = await db.Assignments
            .Where(g => g.State == AssignmentState.Active || g.State == AssignmentState.Closed)
            .Where(g => g.CategoryId == null || g.Category!.Students.Any(s => s.Id == me))
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new
            {
                g.Id, g.Title, g.Description, g.AssignmentType, g.State, g.TimeLimitSeconds, g.XpReward,
                g.CategoryId,
                CategoryName = g.Category != null ? g.Category.Name : null,
                QuestionCount = g.Questions.Count,
                Attempt = g.Attempts
                    .Where(a => a.StudentId == me)
                    .Select(a => new { a.Status, a.Score, a.MaxScore, a.EarnedXp })
                    .FirstOrDefault()
            })
            .ToListAsync();

        return rows.Select(r => new StudentAssignmentSummaryDto(
            r.Id, r.Title, r.Description, r.AssignmentType, r.State, r.TimeLimitSeconds, r.XpReward,
            r.QuestionCount,
            r.Attempt == null ? "NotStarted" : r.Attempt.Status.ToString(),
            r.Attempt?.Score, r.Attempt?.MaxScore, r.Attempt?.EarnedXp,
            r.CategoryId, r.CategoryName)).ToList();
    }

    /// <summary>
    /// Starts (or resumes an in-progress) attempt. StartedAt is recorded server-side;
    /// a finished attempt makes this endpoint return 409 — the one-attempt lock.
    /// </summary>
    [HttpPost("{id:int}/start")]
    public async Task<ActionResult<StartAttemptResponse>> Start(int id)
    {
        var assignment = await db.Assignments
            .Include(g => g.Questions)
            .FirstOrDefaultAsync(g => g.Id == id && g.State == AssignmentState.Active);
        if (assignment is null)
            return NotFound();

        var attempt = await db.StudentAttempts
            .FirstOrDefaultAsync(a => a.AssignmentId == id && a.StudentId == StudentId);

        if (attempt is null)
        {
            attempt = new StudentAttempt
            {
                AssignmentId = id,
                StudentId = StudentId,
                MaxScore = assignment.Questions.Sum(q => q.Points)
            };
            db.StudentAttempts.Add(attempt);
            await db.SaveChangesAsync();
        }
        else if (attempt.Status != AttemptStatus.InProgress)
        {
            return Conflict(new { message = "You have already completed this assignment." });
        }

        var questions = assignment.Questions
            .OrderBy(q => q.Order)
            .Select(q => new StudentQuestionDto(
                q.Id, q.Order, q.Prompt, q.Points,
                GradingService.SanitizeForStudent(assignment.AssignmentType, q.JsonContent)))
            .ToList();

        DateTime? deadline = assignment.IsTimed
            ? attempt.StartedAt.AddSeconds(assignment.TimeLimitSeconds!.Value)
            : null;

        return new StartAttemptResponse(
            attempt.Id, attempt.StartedAt, assignment.TimeLimitSeconds, deadline, questions);
    }

    /// <summary>
    /// Submits answers, validates the countdown window (limit + grace) and grades.
    /// Perfect auto-grades finalize immediately with XP; imperfect fill-in-the-blanks
    /// go to PendingReview with provisional XP the teacher can adjust.
    /// </summary>
    [HttpPost("{id:int}/submit")]
    public async Task<ActionResult<AttemptResultDto>> Submit(int id, SubmitAttemptRequest request)
    {
        var assignment = await db.Assignments
            .Include(g => g.Questions)
            .FirstOrDefaultAsync(g => g.Id == id && g.State == AssignmentState.Active);
        if (assignment is null)
            return NotFound();

        var attempt = await db.StudentAttempts
            .FirstOrDefaultAsync(a => a.AssignmentId == id && a.StudentId == StudentId);
        if (attempt is null)
            return Conflict(new { message = "Start the assignment before submitting." });
        if (attempt.Status != AttemptStatus.InProgress)
            return Conflict(new { message = "This attempt was already submitted." });

        attempt.SubmittedAt = DateTime.UtcNow;
        attempt.AnswersJson = JsonSerializer.Serialize(request.Answers, JsonOptions);
        attempt.MaxScore = assignment.Questions.Sum(q => q.Points);

        var elapsedSeconds = (attempt.SubmittedAt.Value - attempt.StartedAt).TotalSeconds;
        if (assignment.IsTimed && elapsedSeconds > assignment.TimeLimitSeconds!.Value + GradingService.GraceSeconds)
        {
            // Payload arrived outside the allowed window: keep the answers for
            // teacher inspection but award nothing.
            attempt.Status = AttemptStatus.Invalidated;
            attempt.Score = 0;
            attempt.EarnedXp = 0;
        }
        else
        {
            var answersByQuestion = new Dictionary<int, JsonElement>();
            foreach (var answer in request.Answers)
                answersByQuestion[answer.QuestionId] = answer.Answer;

            var score = 0;
            foreach (var question in assignment.Questions)
            {
                JsonElement? answer = answersByQuestion.TryGetValue(question.Id, out var a) ? a : null;
                score += GradingService.Grade(assignment.AssignmentType, question.JsonContent, answer, question.Points);
            }

            attempt.Score = score;
            // The assignment's RequireFeedback toggle decides grading mode: manual assignments
            // park every attempt for teacher review; otherwise the auto grade is final.
            attempt.Status = assignment.RequireFeedback
                ? AttemptStatus.PendingReview
                : AttemptStatus.Completed;
            attempt.EarnedXp = attempt.MaxScore == 0
                ? 0
                : (int)Math.Round(assignment.XpReward * (double)score / attempt.MaxScore);

            var student = (await db.Users.FindAsync(StudentId))!;
            student.TotalXp += attempt.EarnedXp;
        }

        await db.SaveChangesAsync();

        return new AttemptResultDto(
            attempt.Id, attempt.Status, attempt.Score, attempt.MaxScore,
            attempt.EarnedXp, attempt.SubmittedAt, attempt.TeacherFeedback);
    }

    /// <summary>The caller's own finalized result for an assignment (e.g. after teacher review).</summary>
    [HttpGet("{id:int}/result")]
    public async Task<ActionResult<AttemptResultDto>> Result(int id)
    {
        var attempt = await db.StudentAttempts
            .FirstOrDefaultAsync(a => a.AssignmentId == id &&
                                      a.StudentId == StudentId &&
                                      a.Status != AttemptStatus.InProgress);
        if (attempt is null)
            return NotFound();

        return new AttemptResultDto(
            attempt.Id, attempt.Status, attempt.Score, attempt.MaxScore,
            attempt.EarnedXp, attempt.SubmittedAt, attempt.TeacherFeedback);
    }

    /// <summary>
    /// Read-only review of the caller's own finalized attempt: their answers,
    /// per-question points, and the full question content including the correct
    /// answers. Only available once the attempt is finalized, so answer keys
    /// are never exposed to a student who could still (re)play.
    /// </summary>
    [HttpGet("{id:int}/answers")]
    public async Task<ActionResult<MyAnswersDto>> MyAnswers(int id)
    {
        var attempt = await db.StudentAttempts
            .Include(a => a.Assignment).ThenInclude(g => g.Questions)
            .FirstOrDefaultAsync(a => a.AssignmentId == id &&
                                      a.StudentId == StudentId &&
                                      a.Status != AttemptStatus.InProgress);
        if (attempt is null)
            return NotFound();

        var assignment = attempt.Assignment;
        var answers = GradingService.ParseAnswers(attempt.AnswersJson);
        var overrides = GradingService.ParseOverrides(attempt.OverridesJson);

        var breakdown = assignment.Questions.OrderBy(q => q.Order).Select(q =>
        {
            JsonElement? answer = answers.TryGetValue(q.Id, out var a) ? a : null;
            var auto = GradingService.Grade(assignment.AssignmentType, q.JsonContent, answer, q.Points);
            var isOverridden = overrides.TryGetValue(q.Id, out var final);
            return new AnswerBreakdownDto(
                q.Id, q.Order, q.Prompt, q.Points, q.JsonContent,
                answer, auto, isOverridden ? final : auto, isOverridden);
        }).ToList();

        return new MyAnswersDto(
            assignment.Id, assignment.Title, assignment.AssignmentType,
            new AttemptResultDto(
                attempt.Id, attempt.Status, attempt.Score, attempt.MaxScore,
                attempt.EarnedXp, attempt.SubmittedAt, attempt.TeacherFeedback),
            breakdown);
    }
}
