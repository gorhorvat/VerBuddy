namespace Backend.Models;

/// <summary>
/// A single question inside a game instance. The variable per-game-type payload
/// (choices, blanks, word pairs, ...) is stored as JSON in <see cref="JsonContent"/>
/// (nvarchar(max)), so new game formats never require a schema migration.
/// Strongly-typed shapes for the JSON live in Models/GameContent/.
/// </summary>
public class Question
{
    public int Id { get; set; }

    /// <summary>Display order within the game instance (1-based).</summary>
    public int Order { get; set; }

    /// <summary>The prompt shown to the student.</summary>
    public string Prompt { get; set; } = null!;

    /// <summary>
    /// Serialized game-type-specific payload, including the correct answers.
    /// NEVER returned raw to student endpoints — student DTOs strip the answer keys.
    /// </summary>
    public string JsonContent { get; set; } = null!;

    /// <summary>Points this question contributes to the attempt score.</summary>
    public int Points { get; set; } = 1;

    // ── Relationships ─────────────────────────────────────────────────────
    public int GameInstanceId { get; set; }
    public GameInstance GameInstance { get; set; } = null!;
}
