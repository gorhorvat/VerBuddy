namespace Backend.Models;

/// <summary>
/// A playable unit created by the teacher (a game or a timed exam).
/// One instance owns an ordered set of <see cref="Question"/> rows.
/// </summary>
public class GameInstance
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public GameType GameType { get; set; }

    public GameState State { get; set; } = GameState.Draft;

    /// <summary>
    /// Optional countdown. Null or 0 = completely untimed.
    /// When set, the backend validates SubmittedAt - StartedAt against
    /// this value plus a small server-side grace period.
    /// </summary>
    public int? TimeLimitSeconds { get; set; }

    /// <summary>XP awarded for completing this instance (scaled by score at grading time).</summary>
    public int XpReward { get; set; } = 100;

    /// <summary>
    /// When true, submissions are not auto-finalized: every attempt goes to
    /// PendingReview and the teacher's manual review decides the final grade.
    /// When false (default), attempts are auto-graded and completed immediately.
    /// </summary>
    public bool RequireFeedback { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Relationships ─────────────────────────────────────────────────────
    public string CreatedByTeacherId { get; set; } = null!;
    public ApplicationUser CreatedByTeacher { get; set; } = null!;

    /// <summary>Folder/class this game is filed under; null renders as "General".</summary>
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public ICollection<Question> Questions { get; set; } = new List<Question>();
    public ICollection<StudentAttempt> Attempts { get; set; } = new List<StudentAttempt>();

    /// <summary>Convenience flag — true when a real countdown applies.</summary>
    public bool IsTimed => TimeLimitSeconds is > 0;
}
