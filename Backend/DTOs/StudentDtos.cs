using System.Text.Json;
using Backend.Models;

namespace Backend.DTOs;

// ─── Student portal payloads: pseudonymous + answer-key-free ──────────────

public sealed record StudentGameSummaryDto(
    int Id,
    string Title,
    string? Description,
    GameType GameType,
    /// <summary>Active = playable now; Closed = past game (read-only).</summary>
    GameState State,
    int? TimeLimitSeconds,
    int XpReward,
    int QuestionCount,
    /// <summary>"NotStarted" | "InProgress" | "Completed" | "PendingReview" | "Invalidated"</summary>
    string MyStatus,
    int? MyScore,
    int? MyMaxScore,
    int? MyEarnedXp,
    /// <summary>Null = General (visible to every student).</summary>
    int? CategoryId,
    string? CategoryName);

/// <summary>Question as students see it — JsonContent has the answer key stripped.</summary>
public sealed record StudentQuestionDto(
    int Id,
    int Order,
    string Prompt,
    int Points,
    string JsonContent);

public sealed record StartAttemptResponse(
    int AttemptId,
    DateTime StartedAtUtc,
    int? TimeLimitSeconds,
    /// <summary>Null for untimed games; the frontend counts down toward this.</summary>
    DateTime? DeadlineUtc,
    List<StudentQuestionDto> Questions);

public sealed record SubmittedAnswer(int QuestionId, JsonElement Answer);

public sealed record SubmitAttemptRequest(List<SubmittedAnswer> Answers);

public sealed record AttemptResultDto(
    int AttemptId,
    AttemptStatus Status,
    int Score,
    int MaxScore,
    int EarnedXp,
    DateTime? SubmittedAtUtc,
    string? TeacherFeedback);

/// <summary>Leaderboard row — DisplayName and XP only, never PII (GDPR).</summary>
public sealed record LeaderboardEntryDto(int Rank, string DisplayName, int TotalXp);

/// <summary>One class (category) board: active students in that class, ranked by XP.</summary>
public sealed record ClassBoardDto(int Id, string Name, List<LeaderboardEntryDto> Entries);

/// <summary>
/// One board per class the caller belongs to (empty for admins or students with
/// no classes) plus the global board across all students.
/// </summary>
public sealed record LeaderboardResponse(
    List<ClassBoardDto> Classes,
    List<LeaderboardEntryDto> GlobalEntries);
