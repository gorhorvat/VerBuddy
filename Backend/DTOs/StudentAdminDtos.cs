using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs;

// ─── Teacher-admin payloads: the ONLY DTOs allowed to carry student PII. ──
// These must never be returned from endpoints reachable by the Student role.

/// <summary>
/// No password field: credentials are generated server-side at activation time
/// and emailed to the student, who must change them on first login.
/// </summary>
public sealed record CreateStudentRequest(
    [Required, MinLength(3), MaxLength(50)] string Username,
    [MaxLength(100)] string? FirstName,
    [MaxLength(100)] string? LastName,
    [EmailAddress, MaxLength(256)] string? Email,
    /// <summary>Optional nickname; a unique one is generated when omitted.</summary>
    [MaxLength(32)] string? DisplayName,
    /// <summary>The student's class; null = unassigned.</summary>
    int? CategoryId);

public sealed record StudentAdminDto(
    string Id,
    string Username,
    string? FirstName,
    string? LastName,
    string? Email,
    string DisplayName,
    int TotalXp,
    int? CategoryId,
    string? CategoryName,
    bool IsActive,
    /// <summary>When the activation email was sent; null = never activated.</summary>
    DateTime? ActivatedAt,
    bool MustChangePassword);

/// <summary>Bulk creation from a teacher-prepared CSV (parsed client-side).</summary>
public sealed record ImportStudentsRequest(
    [Required, MinLength(1)] List<ImportStudentRow> Rows,
    int? CategoryId);

public sealed record ImportStudentRow(
    [Required, MinLength(3), MaxLength(50)] string Username,
    [MaxLength(100)] string? FirstName,
    [MaxLength(100)] string? LastName,
    [EmailAddress, MaxLength(256)] string? Email,
    [MaxLength(32)] string? DisplayName);

public sealed record ImportStudentsResult(
    List<StudentAdminDto> Created,
    List<string> Errors);

public sealed record BulkActivateRequest([Required, MinLength(1)] List<string> StudentIds);

public sealed record BulkActivateResult(int Activated, List<string> Errors);
