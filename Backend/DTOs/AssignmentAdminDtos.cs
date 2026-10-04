using System.ComponentModel.DataAnnotations;
using Backend.Models;

namespace Backend.DTOs;

// ─── Teacher-admin assignment management payloads ───────────────────────────────

public sealed record CreateAssignmentRequest(
    [Required, MaxLength(200)] string Title,
    [MaxLength(1000)] string? Description,
    AssignmentType AssignmentType,
    /// <summary>Null or 0 = untimed. Capped at 4 hours.</summary>
    [Range(0, 14400)] int? TimeLimitSeconds,
    [Range(0, 10000)] int XpReward = 100,
    /// <summary>When true, attempts await teacher review instead of auto-grading.</summary>
    bool RequireFeedback = false,
    int? CategoryId = null);

public sealed record UpdateAssignmentRequest(
    [Required, MaxLength(200)] string Title,
    [MaxLength(1000)] string? Description,
    [Range(0, 14400)] int? TimeLimitSeconds,
    [Range(0, 10000)] int XpReward,
    bool RequireFeedback,
    int? CategoryId);

public sealed record ChangeAssignmentStateRequest(AssignmentState State);

public sealed record QuestionRequest(
    [Required, MaxLength(2000)] string Prompt,
    [Range(1, 1000)] int Order,
    [Range(1, 1000)] int Points,
    /// <summary>Assignment-type-specific payload; validated by QuestionContentValidator.</summary>
    [Required] string JsonContent);

public sealed record QuestionAdminDto(
    int Id,
    int Order,
    string Prompt,
    int Points,
    string JsonContent);

public sealed record AssignmentSummaryDto(
    int Id,
    string Title,
    string? Description,
    AssignmentType AssignmentType,
    AssignmentState State,
    int? TimeLimitSeconds,
    int XpReward,
    bool RequireFeedback,
    int? CategoryId,
    string? CategoryName,
    DateTime CreatedAt,
    int QuestionCount,
    int AttemptCount,
    /// <summary>Display names of students who attempted, in start order (for the avatar stack).</summary>
    List<string> AttemptDisplayNames);

public sealed record AssignmentDetailDto(
    int Id,
    string Title,
    string? Description,
    AssignmentType AssignmentType,
    AssignmentState State,
    int? TimeLimitSeconds,
    int XpReward,
    bool RequireFeedback,
    int? CategoryId,
    string? CategoryName,
    DateTime CreatedAt,
    int AttemptCount,
    List<QuestionAdminDto> Questions);
