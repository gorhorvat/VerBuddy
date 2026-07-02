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
/// Teacher-only game instance management: CRUD, question configuration and
/// lifecycle transitions (Draft → Active → Closed). Question content is only
/// editable while the game is in Draft, so active/graded games stay immutable.
/// </summary>
[ApiController]
[Route("api/admin/games")]
[Authorize(Roles = AppRoles.AdminOrSuperAdmin)]
public class AdminGamesController(AppDbContext db) : ControllerBase
{
    private string TeacherId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ─── Game instances ────────────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<List<GameSummaryDto>>> List()
    {
        return await db.GameInstances
            .Where(g => g.CreatedByTeacherId == TeacherId)
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new GameSummaryDto(
                g.Id, g.Title, g.Description, g.GameType, g.State,
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
    public async Task<ActionResult<GameDetailDto>> Get(int id)
    {
        var game = await db.GameInstances
            .Include(g => g.Questions.OrderBy(q => q.Order))
            .Include(g => g.Category)
            .FirstOrDefaultAsync(g => g.Id == id && g.CreatedByTeacherId == TeacherId);
        if (game is null)
            return NotFound();

        return ToDetailDto(game, await db.StudentAttempts.CountAsync(a => a.GameInstanceId == id));
    }

    [HttpPost]
    public async Task<ActionResult<GameDetailDto>> Create(CreateGameRequest request)
    {
        if (!await OwnsCategoryAsync(request.CategoryId))
            return BadRequest(new { message = "Unknown category." });

        var game = new GameInstance
        {
            Title = request.Title,
            Description = request.Description,
            GameType = request.GameType,
            State = GameState.Draft,
            TimeLimitSeconds = request.TimeLimitSeconds,
            XpReward = request.XpReward,
            RequireFeedback = request.RequireFeedback,
            CategoryId = request.CategoryId,
            CreatedByTeacherId = TeacherId
        };

        db.GameInstances.Add(game);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = game.Id }, ToDetailDto(game, 0));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<GameDetailDto>> Update(int id, UpdateGameRequest request)
    {
        var game = await FindOwnGameAsync(id);
        if (game is null)
            return NotFound();

        // Title/description/category fixes are always allowed; the fairness-relevant
        // settings (timer, XP, grading mode) are frozen only while students can
        // actively play — Draft and Closed games are fully editable.
        if (game.State == GameState.Active &&
            (game.TimeLimitSeconds != request.TimeLimitSeconds ||
             game.XpReward != request.XpReward ||
             game.RequireFeedback != request.RequireFeedback))
        {
            return Conflict(new { message = "Timer, XP reward and grading mode cannot be changed while the game is Active. Close it first." });
        }

        if (!await OwnsCategoryAsync(request.CategoryId))
            return BadRequest(new { message = "Unknown category." });

        game.Title = request.Title;
        game.Description = request.Description;
        game.TimeLimitSeconds = request.TimeLimitSeconds;
        game.XpReward = request.XpReward;
        game.RequireFeedback = request.RequireFeedback;
        game.CategoryId = request.CategoryId;
        await db.SaveChangesAsync();

        return ToDetailDto(game, await db.StudentAttempts.CountAsync(a => a.GameInstanceId == id));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var game = await FindOwnGameAsync(id);
        if (game is null)
            return NotFound();

        if (await db.StudentAttempts.AnyAsync(a => a.GameInstanceId == id))
            return Conflict(new { message = "A game with recorded attempts cannot be deleted. Close it instead." });

        db.GameInstances.Remove(game); // Questions cascade-delete.
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Lifecycle transitions: Draft→Active (needs ≥1 question), Active→Closed,
    /// Closed→Active (reopen), Active→Draft (only while no attempts exist).
    /// </summary>
    [HttpPost("{id:int}/state")]
    public async Task<ActionResult<GameDetailDto>> ChangeState(int id, ChangeGameStateRequest request)
    {
        var game = await db.GameInstances
            .Include(g => g.Questions)
            .FirstOrDefaultAsync(g => g.Id == id && g.CreatedByTeacherId == TeacherId);
        if (game is null)
            return NotFound();

        var attemptCount = await db.StudentAttempts.CountAsync(a => a.GameInstanceId == id);

        var error = (game.State, request.State) switch
        {
            var (from, to) when from == to => "The game is already in that state.",
            (GameState.Draft, GameState.Active) when game.Questions.Count == 0
                => "Add at least one question before activating.",
            (GameState.Draft, GameState.Active) => null,
            (GameState.Active, GameState.Closed) => null,
            (GameState.Closed, GameState.Active) => null,
            (GameState.Active, GameState.Draft) when attemptCount > 0
                => "Cannot move back to Draft: students have already attempted this game.",
            (GameState.Active, GameState.Draft) => null,
            _ => $"Transition {game.State} → {request.State} is not allowed."
        };
        if (error is not null)
            return Conflict(new { message = error });

        game.State = request.State;
        await db.SaveChangesAsync();

        return ToDetailDto(game, attemptCount);
    }

    /// <summary>Re-files the game into another category (or General), in any state.</summary>
    [HttpPost("{id:int}/category")]
    public async Task<ActionResult<GameDetailDto>> ChangeCategory(int id, ChangeCategoryRequest request)
    {
        var game = await db.GameInstances
            .Include(g => g.Questions)
            .FirstOrDefaultAsync(g => g.Id == id && g.CreatedByTeacherId == TeacherId);
        if (game is null)
            return NotFound();
        if (!await OwnsCategoryAsync(request.CategoryId))
            return BadRequest(new { message = "Unknown category." });

        game.CategoryId = request.CategoryId;
        await db.SaveChangesAsync();
        await db.Entry(game).Reference(g => g.Category).LoadAsync();

        return ToDetailDto(game, await db.StudentAttempts.CountAsync(a => a.GameInstanceId == id));
    }

    /// <summary>
    /// Every student's every answer for this game, with the answer key and the
    /// per-question auto/override points — feeds the teacher's answers view.
    /// </summary>
    [HttpGet("{id:int}/answers")]
    public async Task<ActionResult<GameAnswersDto>> Answers(int id)
    {
        var game = await db.GameInstances
            .Include(g => g.Questions.OrderBy(q => q.Order))
            .FirstOrDefaultAsync(g => g.Id == id && g.CreatedByTeacherId == TeacherId);
        if (game is null)
            return NotFound();

        var attempts = await db.StudentAttempts
            .Where(a => a.GameInstanceId == id && a.Status != AttemptStatus.InProgress)
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
                game.Questions.OrderBy(q => q.Order).Select(q =>
                {
                    System.Text.Json.JsonElement? answer =
                        answers.TryGetValue(q.Id, out var ans) ? ans : null;
                    var auto = GradingService.Grade(game.GameType, q.JsonContent, answer, q.Points);
                    var isOverridden = overrides.TryGetValue(q.Id, out var final);
                    return new AnswerBreakdownDto(
                        q.Id, q.Order, q.Prompt, q.Points, q.JsonContent,
                        answer, auto, isOverridden ? final : auto, isOverridden);
                }).ToList());
        }).ToList();

        return new GameAnswersDto(game.Id, game.Title, game.GameType, game.XpReward, attemptDtos);
    }

    // ─── Questions (editable unless the game is Active) ────────────────────

    [HttpPost("{id:int}/questions")]
    public async Task<ActionResult<QuestionAdminDto>> AddQuestion(int id, QuestionRequest request)
    {
        var game = await FindOwnGameAsync(id);
        if (game is null)
            return NotFound();
        if (game.State == GameState.Active)
            return Conflict(new { message = "Questions cannot be added while the game is Active. Close it first." });

        var contentError = GameContentValidator.Validate(game.GameType, request.JsonContent);
        if (contentError is not null)
            return BadRequest(new { message = contentError });

        var question = new Question
        {
            GameInstanceId = id,
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
        var (game, question) = await FindOwnQuestionAsync(id, questionId);
        if (game is null || question is null)
            return NotFound();
        if (game.State == GameState.Active)
            return Conflict(new { message = "Questions cannot be edited while the game is Active. Close it first." });

        var contentError = GameContentValidator.Validate(game.GameType, request.JsonContent);
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
        var (game, question) = await FindOwnQuestionAsync(id, questionId);
        if (game is null || question is null)
            return NotFound();
        if (game.State == GameState.Active)
            return Conflict(new { message = "Questions cannot be deleted while the game is Active. Close it first." });

        db.Questions.Remove(question);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    private Task<GameInstance?> FindOwnGameAsync(int id) =>
        db.GameInstances
            .Include(g => g.Category)
            .FirstOrDefaultAsync(g => g.Id == id && g.CreatedByTeacherId == TeacherId);

    /// <summary>Null (General) is always allowed; otherwise the category must be the teacher's.</summary>
    private async Task<bool> OwnsCategoryAsync(int? categoryId) =>
        categoryId is null ||
        await db.Categories.AnyAsync(c => c.Id == categoryId && c.TeacherId == TeacherId);

    private async Task<(GameInstance?, Question?)> FindOwnQuestionAsync(int gameId, int questionId)
    {
        var game = await FindOwnGameAsync(gameId);
        if (game is null)
            return (null, null);
        var question = await db.Questions
            .FirstOrDefaultAsync(q => q.Id == questionId && q.GameInstanceId == gameId);
        return (game, question);
    }

    private static GameDetailDto ToDetailDto(GameInstance g, int attemptCount) =>
        new(g.Id, g.Title, g.Description, g.GameType, g.State,
            g.TimeLimitSeconds, g.XpReward, g.RequireFeedback,
            g.CategoryId, g.Category?.Name, g.CreatedAt, attemptCount,
            g.Questions.OrderBy(q => q.Order).Select(ToQuestionDto).ToList());

    private static QuestionAdminDto ToQuestionDto(Question q) =>
        new(q.Id, q.Order, q.Prompt, q.Points, q.JsonContent);
}
