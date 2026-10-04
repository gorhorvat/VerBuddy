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
/// Teacher-only assignment instance management: CRUD, question configuration and
/// lifecycle transitions (Draft → Active → Closed). Question content is only
/// editable while the assignment is in Draft, so active/graded assignments stay immutable.
/// </summary>
[ApiController]
[Route("api/admin/assignments")]
[Authorize(Roles = AppRoles.AdminOrSuperAdmin)]
public class AdminAssignmentsController(AppDbContext db) : ControllerBase
{
    private string TeacherId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ─── Assignment instances ────────────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<List<AssignmentSummaryDto>>> List()
    {
        return await db.Assignments
            .Where(g => g.CreatedByTeacherId == TeacherId)
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new AssignmentSummaryDto(
                g.Id, g.Title, g.Description, g.AssignmentType, g.State,
                g.TimeLimitSeconds, g.XpReward, g.RequireFeedback,
                g.CategoryId, g.Category != null ? g.Category.Name : null, g.CreatedAt,
                g.Questions.Count, g.Attempts.Count,
                g.Attempts
                    .OrderBy(a => a.StartedAt)
                    .Select(a => a.Student.DisplayName)
                    .ToList()))
            .ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AssignmentDetailDto>> Get(int id)
    {
        var assignment = await db.Assignments
            .Include(g => g.Questions.OrderBy(q => q.Order))
            .Include(g => g.Category)
            .FirstOrDefaultAsync(g => g.Id == id && g.CreatedByTeacherId == TeacherId);
        if (assignment is null)
            return NotFound();

        return ToDetailDto(assignment, await db.StudentAttempts.CountAsync(a => a.AssignmentId == id));
    }

    [HttpPost]
    public async Task<ActionResult<AssignmentDetailDto>> Create(CreateAssignmentRequest request)
    {
        if (!await OwnsCategoryAsync(request.CategoryId))
            return BadRequest(new { message = "Unknown category." });

        var assignment = new Assignment
        {
            Title = request.Title,
            Description = request.Description,
            AssignmentType = request.AssignmentType,
            State = AssignmentState.Draft,
            TimeLimitSeconds = request.TimeLimitSeconds,
            XpReward = request.XpReward,
            RequireFeedback = request.RequireFeedback,
            CategoryId = request.CategoryId,
            CreatedByTeacherId = TeacherId
        };

        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = assignment.Id }, ToDetailDto(assignment, 0));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AssignmentDetailDto>> Update(int id, UpdateAssignmentRequest request)
    {
        var assignment = await FindOwnAssignmentAsync(id);
        if (assignment is null)
            return NotFound();

        // Title/description/category fixes are always allowed; the fairness-relevant
        // settings (timer, XP, grading mode) are frozen only while students can
        // actively play — Draft and Closed assignments are fully editable.
        if (assignment.State == AssignmentState.Active &&
            (assignment.TimeLimitSeconds != request.TimeLimitSeconds ||
             assignment.XpReward != request.XpReward ||
             assignment.RequireFeedback != request.RequireFeedback))
        {
            return Conflict(new { message = "Timer, XP reward and grading mode cannot be changed while the assignment is Active. Close it first." });
        }

        if (!await OwnsCategoryAsync(request.CategoryId))
            return BadRequest(new { message = "Unknown category." });

        assignment.Title = request.Title;
        assignment.Description = request.Description;
        assignment.TimeLimitSeconds = request.TimeLimitSeconds;
        assignment.XpReward = request.XpReward;
        assignment.RequireFeedback = request.RequireFeedback;
        assignment.CategoryId = request.CategoryId;
        await db.SaveChangesAsync();

        return ToDetailDto(assignment, await db.StudentAttempts.CountAsync(a => a.AssignmentId == id));
    }

    /// <summary>
    /// Duplicates an assignment (including its questions and answer keys) as a new
    /// Draft — "use as template" for building a variant without touching the
    /// original.
    /// </summary>
    [HttpPost("{id:int}/duplicate")]
    public async Task<ActionResult<AssignmentDetailDto>> Duplicate(int id)
    {
        var assignment = await db.Assignments
            .Include(g => g.Questions)
            .Include(g => g.Category)
            .FirstOrDefaultAsync(g => g.Id == id && g.CreatedByTeacherId == TeacherId);
        if (assignment is null)
            return NotFound();

        var copy = new Assignment
        {
            Title = assignment.Title + " (copy)",
            Description = assignment.Description,
            AssignmentType = assignment.AssignmentType,
            State = AssignmentState.Draft,
            TimeLimitSeconds = assignment.TimeLimitSeconds,
            XpReward = assignment.XpReward,
            RequireFeedback = assignment.RequireFeedback,
            CategoryId = assignment.CategoryId,
            CreatedByTeacherId = TeacherId,
            Questions = assignment.Questions.Select(q => new Question
            {
                Prompt = q.Prompt,
                Order = q.Order,
                Points = q.Points,
                JsonContent = q.JsonContent
            }).ToList()
        };

        db.Assignments.Add(copy);
        await db.SaveChangesAsync();
        copy.Category = assignment.Category;

        return CreatedAtAction(nameof(Get), new { id = copy.Id }, ToDetailDto(copy, 0));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var assignment = await FindOwnAssignmentAsync(id);
        if (assignment is null)
            return NotFound();

        if (await db.StudentAttempts.AnyAsync(a => a.AssignmentId == id))
            return Conflict(new { message = "An assignment with recorded attempts cannot be deleted. Close it instead." });

        db.Assignments.Remove(assignment); // Questions cascade-delete.
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Lifecycle transitions: Draft→Active (needs ≥1 question), Active→Closed,
    /// Closed→Active (reopen), Active→Draft (only while no attempts exist).
    /// </summary>
    [HttpPost("{id:int}/state")]
    public async Task<ActionResult<AssignmentDetailDto>> ChangeState(int id, ChangeAssignmentStateRequest request)
    {
        var assignment = await db.Assignments
            .Include(g => g.Questions)
            .FirstOrDefaultAsync(g => g.Id == id && g.CreatedByTeacherId == TeacherId);
        if (assignment is null)
            return NotFound();

        var attemptCount = await db.StudentAttempts.CountAsync(a => a.AssignmentId == id);

        var error = (assignment.State, request.State) switch
        {
            var (from, to) when from == to => "The assignment is already in that state.",
            (AssignmentState.Draft, AssignmentState.Active) when assignment.Questions.Count == 0
                => "Add at least one question before activating.",
            (AssignmentState.Draft, AssignmentState.Active) => null,
            (AssignmentState.Active, AssignmentState.Closed) => null,
            (AssignmentState.Closed, AssignmentState.Active) => null,
            (AssignmentState.Active, AssignmentState.Draft) when attemptCount > 0
                => "Cannot move back to Draft: students have already attempted this assignment.",
            (AssignmentState.Active, AssignmentState.Draft) => null,
            _ => $"Transition {assignment.State} → {request.State} is not allowed."
        };
        if (error is not null)
            return Conflict(new { message = error });

        assignment.State = request.State;
        await db.SaveChangesAsync();

        return ToDetailDto(assignment, attemptCount);
    }

    /// <summary>Re-files the assignment into another category (or General), in any state.</summary>
    [HttpPost("{id:int}/category")]
    public async Task<ActionResult<AssignmentDetailDto>> ChangeCategory(int id, ChangeCategoryRequest request)
    {
        var assignment = await db.Assignments
            .Include(g => g.Questions)
            .FirstOrDefaultAsync(g => g.Id == id && g.CreatedByTeacherId == TeacherId);
        if (assignment is null)
            return NotFound();
        if (!await OwnsCategoryAsync(request.CategoryId))
            return BadRequest(new { message = "Unknown category." });

        assignment.CategoryId = request.CategoryId;
        await db.SaveChangesAsync();
        await db.Entry(assignment).Reference(g => g.Category).LoadAsync();

        return ToDetailDto(assignment, await db.StudentAttempts.CountAsync(a => a.AssignmentId == id));
    }

    /// <summary>
    /// Every student's every answer for this assignment, with the answer key and the
    /// per-question auto/override points — feeds the teacher's answers view.
    /// </summary>
    [HttpGet("{id:int}/answers")]
    public async Task<ActionResult<AssignmentAnswersDto>> Answers(int id)
    {
        var assignment = await db.Assignments
            .Include(g => g.Questions.OrderBy(q => q.Order))
            .FirstOrDefaultAsync(g => g.Id == id && g.CreatedByTeacherId == TeacherId);
        if (assignment is null)
            return NotFound();

        var attempts = await db.StudentAttempts
            .Where(a => a.AssignmentId == id && a.Status != AttemptStatus.InProgress)
            .Include(a => a.Student)
            .OrderBy(a => a.Student.DisplayName)
            .ToListAsync();

        var attemptDtos = attempts.Select(a =>
        {
            var answers = GradingService.ParseAnswers(a.AnswersJson);
            var overrides = GradingService.ParseOverrides(a.OverridesJson);
            return new AttemptAnswersDto(
                a.Id, a.Student.DisplayName, a.Student.FirstName, a.Student.LastName,
                a.Status, a.Score, a.MaxScore, a.EarnedXp, a.SubmittedAt,
                assignment.Questions.OrderBy(q => q.Order).Select(q =>
                {
                    System.Text.Json.JsonElement? answer =
                        answers.TryGetValue(q.Id, out var ans) ? ans : null;
                    var auto = GradingService.Grade(assignment.AssignmentType, q.JsonContent, answer, q.Points);
                    var isOverridden = overrides.TryGetValue(q.Id, out var final);
                    return new AnswerBreakdownDto(
                        q.Id, q.Order, q.Prompt, q.Points, q.JsonContent,
                        answer, auto, isOverridden ? final : auto, isOverridden);
                }).ToList());
        }).ToList();

        return new AssignmentAnswersDto(assignment.Id, assignment.Title, assignment.AssignmentType, assignment.XpReward, attemptDtos);
    }

    // ─── Questions (editable unless the assignment is Active) ────────────────────

    [HttpPost("{id:int}/questions")]
    public async Task<ActionResult<QuestionAdminDto>> AddQuestion(int id, QuestionRequest request)
    {
        var assignment = await FindOwnAssignmentAsync(id);
        if (assignment is null)
            return NotFound();
        if (assignment.State == AssignmentState.Active)
            return Conflict(new { message = "Questions cannot be added while the assignment is Active. Close it first." });

        var contentError = QuestionContentValidator.Validate(assignment.AssignmentType, request.JsonContent);
        if (contentError is not null)
            return BadRequest(new { message = contentError });

        var question = new Question
        {
            AssignmentId = id,
            Prompt = request.Prompt,
            Order = request.Order,
            Points = request.Points,
            JsonContent = request.JsonContent
        };
        db.Questions.Add(question);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id }, ToQuestionDto(question));
    }

    [HttpPut("{id:int}/questions/{questionId:int}")]
    public async Task<ActionResult<QuestionAdminDto>> UpdateQuestion(int id, int questionId, QuestionRequest request)
    {
        var (assignment, question) = await FindOwnQuestionAsync(id, questionId);
        if (assignment is null || question is null)
            return NotFound();
        if (assignment.State == AssignmentState.Active)
            return Conflict(new { message = "Questions cannot be edited while the assignment is Active. Close it first." });

        var contentError = QuestionContentValidator.Validate(assignment.AssignmentType, request.JsonContent);
        if (contentError is not null)
            return BadRequest(new { message = contentError });

        question.Prompt = request.Prompt;
        question.Order = request.Order;
        question.Points = request.Points;
        question.JsonContent = request.JsonContent;
        await db.SaveChangesAsync();

        return ToQuestionDto(question);
    }

    [HttpDelete("{id:int}/questions/{questionId:int}")]
    public async Task<IActionResult> DeleteQuestion(int id, int questionId)
    {
        var (assignment, question) = await FindOwnQuestionAsync(id, questionId);
        if (assignment is null || question is null)
            return NotFound();
        if (assignment.State == AssignmentState.Active)
            return Conflict(new { message = "Questions cannot be deleted while the assignment is Active. Close it first." });

        db.Questions.Remove(question);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    private Task<Assignment?> FindOwnAssignmentAsync(int id) =>
        db.Assignments
            .Include(g => g.Category)
            .FirstOrDefaultAsync(g => g.Id == id && g.CreatedByTeacherId == TeacherId);

    /// <summary>Null (General) is always allowed; otherwise the category must be the teacher's.</summary>
    private async Task<bool> OwnsCategoryAsync(int? categoryId) =>
        categoryId is null ||
        await db.Categories.AnyAsync(c => c.Id == categoryId && c.TeacherId == TeacherId);

    private async Task<(Assignment?, Question?)> FindOwnQuestionAsync(int assignmentId, int questionId)
    {
        var assignment = await FindOwnAssignmentAsync(assignmentId);
        if (assignment is null)
            return (null, null);
        var question = await db.Questions
            .FirstOrDefaultAsync(q => q.Id == questionId && q.AssignmentId == assignmentId);
        return (assignment, question);
    }

    private static AssignmentDetailDto ToDetailDto(Assignment g, int attemptCount) =>
        new(g.Id, g.Title, g.Description, g.AssignmentType, g.State,
            g.TimeLimitSeconds, g.XpReward, g.RequireFeedback,
            g.CategoryId, g.Category?.Name, g.CreatedAt, attemptCount,
            g.Questions.OrderBy(q => q.Order).Select(ToQuestionDto).ToList());

    private static QuestionAdminDto ToQuestionDto(Question q) =>
        new(q.Id, q.Order, q.Prompt, q.Points, q.JsonContent);
}
