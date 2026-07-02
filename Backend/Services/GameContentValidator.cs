using System.Text.Json;
using Backend.Models;
using Backend.Models.GameContent;

namespace Backend.Services;

/// <summary>
/// Validates a question's JsonContent against the strongly-typed shape for its
/// game type before it is stored. Keeps the flexible nvarchar(max) column from
/// ever holding malformed or ungradeable payloads.
/// </summary>
public static class GameContentValidator
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    /// <summary>Returns an error message, or null when the content is valid.</summary>
    public static string? Validate(GameType gameType, string jsonContent)
    {
        try
        {
            return gameType switch
            {
                GameType.SingleChoice => ValidateSingleChoice(jsonContent),
                GameType.MultipleChoice => ValidateMultipleChoice(jsonContent),
                GameType.FillInTheBlanks => ValidateFillInTheBlanks(jsonContent),
                GameType.WordMatching => ValidateWordMatching(jsonContent),
                _ => $"Unknown game type '{gameType}'."
            };
        }
        catch (JsonException ex)
        {
            return $"Content is not valid JSON: {ex.Message}";
        }
    }

    private static string? ValidateSingleChoice(string json)
    {
        var content = Deserialize<SingleChoiceContent>(json);
        if (content is null) return "Content is empty.";
        if (content.Choices.Count < 2) return "Single choice needs at least 2 choices.";
        if (content.Choices.Any(string.IsNullOrWhiteSpace)) return "Choices must not be empty.";
        if (content.CorrectIndex < 0 || content.CorrectIndex >= content.Choices.Count)
            return $"correctIndex must be between 0 and {content.Choices.Count - 1}.";
        return null;
    }

    private static string? ValidateMultipleChoice(string json)
    {
        var content = Deserialize<MultipleChoiceContent>(json);
        if (content is null) return "Content is empty.";
        if (content.Choices.Count < 2) return "Multiple choice needs at least 2 choices.";
        if (content.Choices.Any(string.IsNullOrWhiteSpace)) return "Choices must not be empty.";
        if (content.CorrectIndexes.Count == 0) return "At least one correct index is required.";
        if (content.CorrectIndexes.Distinct().Count() != content.CorrectIndexes.Count)
            return "correctIndexes must not contain duplicates.";
        if (content.CorrectIndexes.Any(i => i < 0 || i >= content.Choices.Count))
            return $"Every correct index must be between 0 and {content.Choices.Count - 1}.";
        return null;
    }

    private static string? ValidateFillInTheBlanks(string json)
    {
        var content = Deserialize<FillInTheBlanksContent>(json);
        if (content is null) return "Content is empty.";
        if (string.IsNullOrWhiteSpace(content.Template)) return "Template must not be empty.";

        var markerCount = content.Template.Split(FillInTheBlanksContent.BlankMarker).Length - 1;
        if (markerCount == 0)
            return $"Template must contain at least one '{FillInTheBlanksContent.BlankMarker}' blank marker.";
        if (markerCount != content.Blanks.Count)
            return $"Template has {markerCount} blank marker(s) but {content.Blanks.Count} blank spec(s) were provided.";
        if (content.Blanks.Any(b => b.AcceptedAnswers.Count == 0 ||
                                    b.AcceptedAnswers.Any(string.IsNullOrWhiteSpace)))
            return "Every blank needs at least one non-empty accepted answer.";
        return null;
    }

    private static string? ValidateWordMatching(string json)
    {
        var content = Deserialize<WordMatchingContent>(json);
        if (content is null) return "Content is empty.";
        if (content.Pairs.Count < 2) return "Word matching needs at least 2 pairs.";
        if (content.Pairs.Any(p => string.IsNullOrWhiteSpace(p.Key) || string.IsNullOrWhiteSpace(p.Value)))
            return "Every pair needs a non-empty key and value.";
        if (content.Pairs.Select(p => p.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != content.Pairs.Count)
            return "Pair keys must be unique.";
        return null;
    }

    private static T? Deserialize<T>(string json) where T : class =>
        JsonSerializer.Deserialize<T>(json, JsonOptions);
}
