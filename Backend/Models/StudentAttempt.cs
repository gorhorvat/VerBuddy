namespace Backend.Models;

/// <summary>
/// One student's single attempt at an assignment instance. A unique index on
/// (AssignmentId, StudentId) enforces the one-attempt lock at the database level.
/// </summary>
public class StudentAttempt
{
    public int Id { get; set; }

    public AttemptStatus Status { get; set; } = AttemptStatus.InProgress;

    /// <summary>Points earned (auto-graded, optionally overridden by the teacher).</summary>
    public int Score { get; set; }

    /// <summary>Maximum points possible at the time of the attempt (sum of question Points).</summary>
    public int MaxScore { get; set; }

    /// <summary>XP granted when the attempt was finalized (0 until then).</summary>
    public int EarnedXp { get; set; }

    /// <summary>The student's raw answers, serialized per assignment type (nvarchar(max)).</summary>
    public string? AnswersJson { get; set; }

    /// <summary>
    /// Teacher per-question point overrides as JSON: { "questionId": points }.
    /// An override replaces the auto-graded points for that question (e.g. to
    /// accept a typo); the attempt Score/XP are recomputed when it changes.
    /// </summary>
    public string? OverridesJson { get; set; }

    /// <summary>Server timestamp (UTC) recorded when the student opened the assignment.</summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Server timestamp (UTC) of submission. For timed assignments the backend invalidates the
    /// attempt when SubmittedAt - StartedAt > TimeLimitSeconds + grace period.
    /// </summary>
    public DateTime? SubmittedAt { get; set; }

    /// <summary>Optional note the teacher leaves when manually overriding a grade.</summary>
    public string? TeacherFeedback { get; set; }

    // ── Relationships ─────────────────────────────────────────────────────
    public int AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;

    public string StudentId { get; set; } = null!;
    public ApplicationUser Student { get; set; } = null!;
}
