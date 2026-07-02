using System.Text.Json;
using Backend.Models;
using Backend.Models.GameContent;

namespace Backend.Services;

/// <summary>
/// Auto-grading for all four game types, plus content sanitization: the payload
/// sent to students must never contain the answer key, so each type has an
/// explicit student-facing projection built here.
/// </summary>
public static class GradingService
{
    /// <summary>Server-side tolerance added to TimeLimitSeconds before invalidating.</summary>
    public const int GraceSeconds = 10;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    // ─── Student-safe content (answer keys stripped) ──────────────────────

    public static string SanitizeForStudent(GameType type, string jsonContent)
    {
        switch (type)
        {
            case GameType.SingleChoice:
            {
                var c = Deserialize<SingleChoiceContent>(jsonContent);
                return JsonSerializer.Serialize(new { choices = c.Choices }, JsonOptions);
            }
            case GameType.MultipleChoice:
            {
                var c = Deserialize<MultipleChoiceContent>(jsonContent);
                // correctCount is a deliberate UX hint ("select 2"), not the key itself.
                return JsonSerializer.Serialize(
                    new { choices = c.Choices, correctCount = c.CorrectIndexes.Count }, JsonOptions);
            }
            case GameType.FillInTheBlanks:
            {
                var c = Deserialize<FillInTheBlanksContent>(jsonContent);
                return JsonSerializer.Serialize(
                    new { template = c.Template, blankCount = c.Blanks.Count }, JsonOptions);
            }
            case GameType.WordMatching:
            {
                var c = Deserialize<WordMatchingContent>(jsonContent);
                var values = c.Pairs.Select(p => p.Value).ToList();
                if (c.ShuffleRightColumn)
                    Shuffle(values);
                return JsonSerializer.Serialize(new
                {
                    instructions = c.Instructions,
                    keys = c.Pairs.Select(p => p.Key).ToList(),
                    values
                }, JsonOptions);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    // ─── Grading ───────────────────────────────────────────────────────────

    /// <summary>Returns the points earned for one question (0 for unanswered or malformed).</summary>
    public static int Grade(GameType type, string jsonContent, JsonElement? answer, int points)
    {
        if (answer is not { } a)
            return 0; // Unanswered question.

        try
        {
            return type switch
            {
                GameType.SingleChoice => GradeSingleChoice(jsonContent, a, points),
                GameType.MultipleChoice => GradeMultipleChoice(jsonContent, a, points),
                GameType.FillInTheBlanks => GradeFillInTheBlanks(jsonContent, a, points),
                GameType.WordMatching => GradeWordMatching(jsonContent, a, points),
                _ => 0
            };
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return 0; // Malformed answer payload grades as zero.
        }
    }

    /// <summary>Answer shape: { "selectedIndex": 1 } — all-or-nothing.</summary>
    private static int GradeSingleChoice(string json, JsonElement a, int points)
    {
        var c = Deserialize<SingleChoiceContent>(json);
        var correct = a.TryGetProperty("selectedIndex", out var idx) &&
                      idx.ValueKind == JsonValueKind.Number &&
                      idx.GetInt32() == c.CorrectIndex;
        return correct ? points : 0;
    }

    /// <summary>Answer shape: { "selectedIndexes": [0, 2] } — exact set match, all-or-nothing.</summary>
    private static int GradeMultipleChoice(string json, JsonElement a, int points)
    {
        var c = Deserialize<MultipleChoiceContent>(json);
        if (!a.TryGetProperty("selectedIndexes", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return 0;

        var selected = arr.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.Number)
            .Select(e => e.GetInt32())
            .ToHashSet();
        return selected.SetEquals(c.CorrectIndexes) ? points : 0;
    }

    /// <summary>Answer shape: { "answers": ["went", "cooked"] } — partial credit per blank.</summary>
    private static int GradeFillInTheBlanks(string json, JsonElement a, int points)
    {
        var c = Deserialize<FillInTheBlanksContent>(json);
        if (!a.TryGetProperty("answers", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return 0;

        var given = arr.EnumerateArray()
            .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : "")
            .ToList();

        var correctCount = 0;
        for (var i = 0; i < c.Blanks.Count; i++)
        {
            var candidate = (i < given.Count ? given[i] : "").Trim();
            var comparison = c.Blanks[i].CaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;
            if (c.Blanks[i].AcceptedAnswers.Any(acc => string.Equals(acc.Trim(), candidate, comparison)))
                correctCount++;
        }

        return ScalePoints(points, correctCount, c.Blanks.Count);
    }

    /// <summary>Answer shape: { "matches": { "generous": "willing to ..." } } — partial credit per pair.</summary>
    private static int GradeWordMatching(string json, JsonElement a, int points)
    {
        var c = Deserialize<WordMatchingContent>(json);
        if (!a.TryGetProperty("matches", out var matches) || matches.ValueKind != JsonValueKind.Object)
            return 0;

        var correctCount = c.Pairs.Count(pair =>
            matches.TryGetProperty(pair.Key, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            string.Equals(value.GetString()?.Trim(), pair.Value.Trim(), StringComparison.Ordinal));

        return ScalePoints(points, correctCount, c.Pairs.Count);
    }

    // ─── Answer parsing & recomputation (breakdown views, overrides) ──────

    /// <summary>Parses StudentAttempt.AnswersJson into a questionId → answer map.</summary>
    public static Dictionary<int, JsonElement> ParseAnswers(string? answersJson)
    {
        if (string.IsNullOrEmpty(answersJson))
            return [];
        var list = JsonSerializer.Deserialize<List<SubmittedAnswerJson>>(answersJson, JsonOptions) ?? [];
        var map = new Dictionary<int, JsonElement>();
        foreach (var entry in list)
            map[entry.QuestionId] = entry.Answer;
        return map;
    }

    /// <summary>Parses StudentAttempt.OverridesJson into a questionId → points map.</summary>
    public static Dictionary<int, int> ParseOverrides(string? overridesJson) =>
        string.IsNullOrEmpty(overridesJson)
            ? []
            : JsonSerializer.Deserialize<Dictionary<int, int>>(overridesJson) ?? [];

    public static string SerializeOverrides(Dictionary<int, int> overrides) =>
        JsonSerializer.Serialize(overrides);

    /// <summary>Final points for one question: teacher override wins over the auto grade.</summary>
    public static int FinalPoints(
        Models.Question question, GameType type,
        Dictionary<int, JsonElement> answers, Dictionary<int, int> overrides)
    {
        if (overrides.TryGetValue(question.Id, out var overridden))
            return overridden;
        JsonElement? answer = answers.TryGetValue(question.Id, out var a) ? a : null;
        return Grade(type, question.JsonContent, answer, question.Points);
    }

    private sealed record SubmittedAnswerJson(int QuestionId, JsonElement Answer);

    // ─── Helpers ───────────────────────────────────────────────────────────

    private static int ScalePoints(int points, int correct, int total) =>
        total == 0 ? 0 : (int)Math.Round(points * (double)correct / total);

    private static T Deserialize<T>(string json) where T : class =>
        JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException("Empty content payload.");

    private static void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
