using Microsoft.AspNetCore.Identity;

namespace Backend.Models;

/// <summary>
/// Identity user covering both roles (Teacher / Student, differentiated via ASP.NET Identity roles).
///
/// GDPR data-minimization contract:
///  - <see cref="FirstName"/>, <see cref="LastName"/> and the inherited Email are PII and may
///    ONLY be projected into admin DTOs and endpoints ([Authorize(Roles = "Admin,SuperAdmin")]).
///  - <see cref="DisplayName"/> is the ONLY identity field allowed in student-facing payloads
///    (leaderboard, peer metrics, attempt feedback).
///  - The inherited UserName is used exclusively for login authentication and is never
///    rendered to peers.
/// </summary>
public class ApplicationUser : IdentityUser
{
    // ── PII: teacher-admin scope only ─────────────────────────────────────
    [PersonalData]
    public string? FirstName { get; set; }

    [PersonalData]
    public string? LastName { get; set; }

    // ── Public, pseudonymous profile ──────────────────────────────────────
    /// <summary>
    /// Nickname generated or assigned at account creation. Unique, and the only
    /// name ever exposed on the classroom leaderboard or any student endpoint.
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Denormalized lifetime XP total, updated transactionally whenever a
    /// StudentAttempt is finalized. Keeps the leaderboard a single indexed read.
    /// </summary>
    public int TotalXp { get; set; }

    /// <summary>The student's classes (teacher categories); empty = unassigned.</summary>
    public ICollection<Category> Categories { get; set; } = new List<Category>();

    /// <summary>
    /// True from activation until the student sets their own password on first
    /// login; the frontend forces a password change while this is set.
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>When the activation email (with first-login credentials) was sent; null = never.</summary>
    public DateTime? ActivatedAt { get; set; }

    /// <summary>Deactivated accounts cannot log in but keep their history.</summary>
    public bool IsActive { get; set; } = true;

    // ── Navigation ────────────────────────────────────────────────────────
    public ICollection<GameInstance> CreatedGames { get; set; } = new List<GameInstance>();
    public ICollection<StudentAttempt> Attempts { get; set; } = new List<StudentAttempt>();
}
