using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Backend.Models;

namespace Backend.DTOs;

// ─── Per-question answer breakdowns ────────────────────────────────────────
// Shared by the teacher's per-assignment answers view and the student's read-only
// review. ContentJson is the FULL question content including the answer key,
// so these DTOs must only ever be returned for finalized attempts (student)
// or to the teacher.

public sealed record AnswerBreakdownDto(
    int QuestionId,
    int Order,
    string Prompt,
    int Points,
    string ContentJson,
    JsonElement? Answer,
    int AutoPoints,
    int FinalPoints,
    bool IsOverridden);

/// <summary>Teacher view: one assignment, all attempts, every answer.</summary>
public sealed record AssignmentAnswersDto(
    int AssignmentId,
    string Title,
    AssignmentType AssignmentType,
    int XpReward,
    List<AttemptAnswersDto> Attempts);

public sealed record AttemptAnswersDto(
    int AttemptId,
    string StudentDisplayName,
    string? StudentFirstName,
    string? StudentLastName,
    AttemptStatus Status,
    int Score,
    int MaxScore,
    int EarnedXp,
    DateTime? SubmittedAtUtc,
    List<AnswerBreakdownDto> Answers);

/// <summary>Student view: their own finalized attempt with the answer key.</summary>
public sealed record MyAnswersDto(
    int AssignmentId,
    string Title,
    AssignmentType AssignmentType,
    AttemptResultDto Result,
    List<AnswerBreakdownDto> Answers);

/// <summary>Teacher accepts/adjusts one answer's points (e.g. a forgivable typo).</summary>
public sealed record OverrideAnswerRequest(
    int QuestionId,
    [Range(0, 1000)] int Points);
