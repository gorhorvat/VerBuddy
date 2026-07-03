using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs;

// ─── Teacher-admin reward management payloads ─────────────────────────────

public sealed record RewardRequest(
    [Required, MaxLength(200)] string Title,
    [MaxLength(1000)] string? Description,
    [Range(0, 99)] int RequiredLevel);

public sealed record RewardDto(
    int Id,
    string Title,
    string? Description,
    int RequiredLevel);

/// <summary>Application row for the teacher's review queue — DisplayName only, never PII.</summary>
public sealed record RewardApplicationDto(
    int Id,
    int RewardId,
    string RewardTitle,
    int RequiredLevel,
    string StudentDisplayName,
    /// <summary>"Pending" | "Approved" | "Denied"</summary>
    string Status,
    DateTime CreatedAtUtc,
    DateTime? DecidedAtUtc);

// ─── Student portal payloads: pseudonymous only ────────────────────────────

/// <summary>
/// A reward as a student sees it: whether their current level unlocks it, and
/// the status of their latest application (null = never applied, or free to
/// (re)apply after a denial).
/// </summary>
public sealed record StudentRewardDto(
    int Id,
    string Title,
    string? Description,
    int RequiredLevel,
    bool Unlocked,
    /// <summary>Latest application's status ("Pending" | "Approved" | "Denied"), or null if never applied.</summary>
    string? MyApplicationStatus);
