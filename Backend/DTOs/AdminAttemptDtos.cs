using System.ComponentModel.DataAnnotations;
using Backend.Models;

namespace Backend.DTOs;

// ─── Teacher-admin attempt payloads (may carry student PII) ───────────────

public sealed record AttemptAdminDto(
    int Id,
    int AssignmentId,
    string AssignmentTitle,
    AssignmentType AssignmentType,
    string StudentDisplayName,
    string? StudentFirstName,
    string? StudentLastName,
    AttemptStatus Status,
    int Score,
    int MaxScore,
    int EarnedXp,
    DateTime StartedAtUtc,
    DateTime? SubmittedAtUtc,
    string? AnswersJson,
    string? TeacherFeedback);

public sealed record ReviewAttemptRequest(
    [Range(0, 100000)] int Score,
    [MaxLength(2000)] string? Feedback);
