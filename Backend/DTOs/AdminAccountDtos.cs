using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs;

// ─── SuperAdmin payloads: admin-account PII is allowed here only. ─────────
// These must never be returned from endpoints reachable by Admin or User roles.

/// <summary>
/// No password field: credentials are generated server-side at activation time
/// and emailed to the admin, who must change them on first login.
/// </summary>
public sealed record CreateAdminRequest(
    [Required, MinLength(3), MaxLength(50)] string Username,
    [MaxLength(100)] string? FirstName,
    [MaxLength(100)] string? LastName,
    [EmailAddress, MaxLength(256)] string? Email,
    /// <summary>Optional nickname; a unique one is generated when omitted.</summary>
    [MaxLength(32)] string? DisplayName);

public sealed record UpdateAdminRequest(
    [MaxLength(100)] string? FirstName,
    [MaxLength(100)] string? LastName,
    [EmailAddress, MaxLength(256)] string? Email,
    [MaxLength(32)] string? DisplayName);

public sealed record AdminAccountDto(
    string Id,
    string Username,
    string? FirstName,
    string? LastName,
    string? Email,
    string DisplayName,
    bool IsActive,
    /// <summary>When the activation email was sent; null = never activated.</summary>
    DateTime? ActivatedAt,
    bool MustChangePassword);
