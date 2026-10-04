namespace Backend.Models;

/// <summary>
/// A single question inside an assignment instance. The variable per-assignment-type payload
/// (choices, blanks, word pairs, ...) is stored as JSON in <see cref="JsonContent"/>
/// (nvarchar(max)), so new assignment formats never require a schema migration.
/// Strongly-typed shapes for the JSON live in Models/QuestionContent/.
/// </summary>
public class Question
{
    public int Id { get; set; }

    /// <summary>Display order within the assignment instance (1-based).</summary>
    public int Order { get; set; }

    /// <summary>The prompt shown to the student.</summary>
    public string Prompt { get; set; } = null!;

    /// <summary>
    /// Serialized assignment-type-specific payload, including the correct answers.
    /// NEVER returned raw to student endpoints — student DTOs strip the answer keys.
    /// </summary>
    public string JsonContent { get; set; } = null!;

    /// <summary>Points this question contributes to the attempt score.</summary>
    public int Points { get; set; } = 1;

    // ── Relationships ─────────────────────────────────────────────────────
    public int AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;
}
