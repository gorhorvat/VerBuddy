namespace Backend.Models.GameContent;

/// <summary>
/// Shape of <see cref="Question.JsonContent"/> for GameType.FillInTheBlanks.
/// The template uses "___" (three underscores) per blank; Blanks[i] grades the
/// i-th occurrence. Auto-graded by string match, with teacher manual override
/// (AttemptStatus.PendingReview) for answers that don't match exactly.
///
/// Example:
/// {
///   "template": "She ___ to school every day, but yesterday she ___ at home.",
///   "blanks": [
///     { "acceptedAnswers": ["goes"], "caseSensitive": false },
///     { "acceptedAnswers": ["stayed", "remained"], "caseSensitive": false }
///   ]
/// }
/// </summary>
public sealed class FillInTheBlanksContent
{
    public const string BlankMarker = "___";

    public string Template { get; set; } = null!;

    public List<BlankSpec> Blanks { get; set; } = [];
}

public sealed class BlankSpec
{
    /// <summary>All answers accepted as correct for this blank (the answer key).</summary>
    public List<string> AcceptedAnswers { get; set; } = [];

    public bool CaseSensitive { get; set; }
}
