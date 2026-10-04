namespace Backend.Models.QuestionContent;

/// <summary>
/// Strongly-typed shape of <see cref="Question.JsonContent"/> for AssignmentType.Matching.
/// Serialized with System.Text.Json into the nvarchar(max) column.
///
/// Example stored JSON:
/// {
///   "instructions": "Match each English word to its meaning.",
///   "shuffleRightColumn": true,
///   "pairs": [
///     { "key": "generous", "value": "willing to give more than expected" },
///     { "key": "reluctant", "value": "unwilling or hesitant to do something" }
///   ]
/// }
/// </summary>
public sealed class MatchingContent
{
    public string Instructions { get; set; } = "Match the pairs.";

    /// <summary>Whether the frontend should shuffle the value column before rendering.</summary>
    public bool ShuffleRightColumn { get; set; } = true;

    /// <summary>The correct key→value pairs (the answer key — stripped from student DTOs).</summary>
    public List<WordPair> Pairs { get; set; } = [];
}

public sealed class WordPair
{
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
}

/// <summary>
/// Shape of <see cref="StudentAttempt.AnswersJson"/> for matching:
/// the student's chosen value for each key. Grading compares each entry
/// against <see cref="MatchingContent.Pairs"/>.
/// </summary>
public sealed class MatchingAnswers
{
    public Dictionary<string, string> Matches { get; set; } = [];
}
