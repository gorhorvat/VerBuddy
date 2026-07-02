using System.Security.Claims;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

/// <summary>
/// Teacher-only attempt monitoring and manual grade override (primarily for
/// fill-in-the-blanks answers in PendingReview). Scoped to the teacher's own games.
/// </summary>
[ApiController]
[Route("api/admin/attempts")]
[Authorize(Roles = AppRoles.AdminOrSuperAdmin)]
public class AdminAttemptsController(AppDbContext db) : ControllerBase
{
    private string TeacherId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>Attempts across the teacher's games; filter by game and/or pending review.</summary>
    [HttpGet]
    public async Task<ActionResult<List<AttemptAdminDto>>> List(
        [FromQuery] int? gameId, [FromQuery] bool pendingOnly = false)
    {
        var query = db.StudentAttempts
            .Where(a => a.GameInstance.CreatedByTeacherId == TeacherId);
        if (gameId is not null)
            query = query.Where(a => a.GameInstanceId == gameId);
        if (pendingOnly)
            query = query.Where(a => a.Status == AttemptStatus.PendingReview);

        return await query
            .OrderByDescending(a => a.StartedAt)
            .Select(a => new AttemptAdminDto(
                a.Id, a.GameInstanceId, a.GameInstance.Title, a.GameInstance.GameType,
                a.Student.DisplayName, a.Student.FirstName, a.Student.LastName,
                a.Status, a.Score, a.MaxScore, a.EarnedXp,
                a.StartedAt, a.SubmittedAt, a.AnswersJson, a.TeacherFeedback))
            .ToListAsync();
    }

    /// <summary>
    /// Accepts or adjusts the points for a single answer (e.g. a forgivable typo).
    /// The attempt's Score, EarnedXp and the student's TotalXp are recomputed from
    /// the auto grades plus all stored overrides, so the leaderboard updates instantly.
    /// </summary>
    [HttpPost("{id:int}/override-answer")]
    public async Task<ActionResult<AttemptAdminDto>> OverrideAnswer(int id, OverrideAnswerRequest request)
    {
        var attempt = await db.StudentAttempts
            .Include(a => a.GameInstance).ThenInclude(g => g.Questions)
            .Include(a => a.Student)
            .FirstOrDefaultAsync(a => a.Id == id &&
                                      a.GameInstance.CreatedByTeacherId == TeacherId);
        if (attempt is null)
            return NotFound();

        if (attempt.Status is AttemptStatus.InProgress or AttemptStatus.Invalidated)
            return Conflict(new { message = $"An attempt in state '{attempt.Status}' cannot be adjusted." });

        var question = attempt.GameInstance.Questions.FirstOrDefault(q => q.Id == request.QuestionId);
        if (question is null)
            return NotFound(new { message = "That question does not belong to this game." });
        if (request.Points > question.Points)
            return BadRequest(new { message = $"Points cannot exceed the question maximum of {question.Points}." });

        var overrides = GradingService.ParseOverrides(attempt.OverridesJson);
        overrides[question.Id] = request.Points;
        attempt.OverridesJson = GradingService.SerializeOverrides(overrides);

        // Recompute the whole attempt from auto grades + overrides.
        var answers = GradingService.ParseAnswers(attempt.AnswersJson);
        attempt.Score = attempt.GameInstance.Questions
            .Sum(q => GradingService.FinalPoints(q, attempt.GameInstance.GameType, answers, overrides));

        var newXp = attempt.MaxScore == 0
            ? 0
            : (int)Math.Round(attempt.GameInstance.XpReward * (double)attempt.Score / attempt.MaxScore);
        attempt.Student.TotalXp += newXp - attempt.EarnedXp;
        attempt.EarnedXp = newXp;

        await db.SaveChangesAsync();

        return new AttemptAdminDto(
            attempt.Id, attempt.GameInstanceId, attempt.GameInstance.Title, attempt.GameInstance.GameType,
            attempt.Student.DisplayName, attempt.Student.FirstName, attempt.Student.LastName,
            attempt.Status, attempt.Score, attempt.MaxScore, attempt.EarnedXp,
            attempt.StartedAt, attempt.SubmittedAt, attempt.AnswersJson, attempt.TeacherFeedback);
    }

    /// <summary>
    /// Manual grade override. Finalizes the attempt as Completed, re-awards XP
    /// proportionally and adjusts the student's TotalXp by the delta, so the
    /// leaderboard and admin panel update instantly.
    /// </summary>
    [HttpPost("{id:int}/review")]
    public async Task<ActionResult<AttemptAdminDto>> Review(int id, ReviewAttemptRequest request)
    {
        var attempt = await db.StudentAttempts
            .Include(a => a.GameInstance)
            .Include(a => a.Student)
            .FirstOrDefaultAsync(a => a.Id == id &&
                                      a.GameInstance.CreatedByTeacherId == TeacherId);
        if (attempt is null)
            return NotFound();

        if (attempt.Status is not (AttemptStatus.PendingReview or AttemptStatus.Completed))
            return Conflict(new { message = $"An attempt in state '{attempt.Status}' cannot be reviewed." });

        if (request.Score > attempt.MaxScore)
            return BadRequest(new { message = $"Score cannot exceed the maximum of {attempt.MaxScore}." });

        attempt.Score = request.Score;
        attempt.TeacherFeedback = request.Feedback;
        attempt.Status = AttemptStatus.Completed;

        var newXp = attempt.MaxScore == 0
            ? 0
            : (int)Math.Round(attempt.GameInstance.XpReward * (double)request.Score / attempt.MaxScore);
        attempt.Student.TotalXp += newXp - attempt.EarnedXp;
        attempt.EarnedXp = newXp;

        await db.SaveChangesAsync();

        return new AttemptAdminDto(
            attempt.Id, attempt.GameInstanceId, attempt.GameInstance.Title, attempt.GameInstance.GameType,
            attempt.Student.DisplayName, attempt.Student.FirstName, attempt.Student.LastName,
            attempt.Status, attempt.Score, attempt.MaxScore, attempt.EarnedXp,
            attempt.StartedAt, attempt.SubmittedAt, attempt.AnswersJson, attempt.TeacherFeedback);
    }
}
