namespace Backend.Models;

/// <summary>
/// The four launch game types. New types can be added without schema changes
/// because question content lives in a JSON column (see <see cref="Question.JsonContent"/>).
/// </summary>
public enum GameType
{
    SingleChoice = 0,
    MultipleChoice = 1,
    FillInTheBlanks = 2,
    WordMatching = 3
}

/// <summary>Lifecycle state of a game instance as controlled by the teacher.</summary>
public enum GameState
{
    Draft = 0,      // Teacher is still configuring content — invisible to students.
    Active = 1,     // Rendered on the Student Portal dashboard, playable.
    Closed = 2      // No longer accepting attempts; results remain visible.
}

/// <summary>State of a single student's attempt at a game instance.</summary>
public enum AttemptStatus
{
    InProgress = 0,     // StartedAt recorded, no submission yet.
    Completed = 1,      // Auto-graded and finalized; XP awarded.
    PendingReview = 2,  // Fill-in-the-blanks awaiting teacher manual override.
    Invalidated = 3     // Submission arrived outside TimeLimitSeconds + grace period.
}
