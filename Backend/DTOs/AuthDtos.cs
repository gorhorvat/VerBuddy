using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs;

// ─── Student-safe payloads: pseudonymous fields ONLY, never PII ───────────

public sealed record LoginRequest(
    [Required] string Username,
    [Required] string Password);

public sealed record AuthResponse(
    string Token,
    DateTime ExpiresAtUtc,
    string DisplayName,
    int TotalXp,
    IList<string> Roles,
    /// <summary>True until the student replaces their first-login password.</summary>
    bool MustChangePassword);

public sealed record MeResponse(
    string DisplayName,
    int TotalXp,
    int Level,
    IList<string> Roles,
    /// <summary>True until the student replaces their first-login password.</summary>
    bool MustChangePassword);

public sealed record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword);

/// <summary>Anonymous reset via the emailed link (userId + Identity reset token).</summary>
public sealed record ResetPasswordRequest(
    [Required] string UserId,
    [Required] string Token,
    [Required, MinLength(8)] string NewPassword);
