namespace Backend.Models.GameContent;

/// <summary>
/// Shape of <see cref="Question.JsonContent"/> for GameType.SingleChoice.
/// Example: { "choices": ["go", "goes", "going"], "correctIndex": 1 }
/// </summary>
public sealed class SingleChoiceContent
{
    public List<string> Choices { get; set; } = [];

    /// <summary>Zero-based index into <see cref="Choices"/> (the answer key).</summary>
    public int CorrectIndex { get; set; }
}

/// <summary>
/// Shape of <see cref="Question.JsonContent"/> for GameType.MultipleChoice.
/// Example: { "choices": ["cat", "dog", "table", "run"], "correctIndexes": [0, 1] }
/// </summary>
public sealed class MultipleChoiceContent
{
    public List<string> Choices { get; set; } = [];

    /// <summary>Zero-based indexes of all correct choices (the answer key).</summary>
    public List<int> CorrectIndexes { get; set; } = [];
}
