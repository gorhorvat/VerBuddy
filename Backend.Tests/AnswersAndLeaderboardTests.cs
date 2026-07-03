using System.Net;
using System.Net.Http.Json;
using Backend.DTOs;
using static Backend.Tests.TestHelpers;

namespace Backend.Tests;

[Collection("Api")]
public class AnswersAndLeaderboardTests(ApiFactory factory)
{
    [Fact]
    public async Task TeacherAnswersView_ShowsPerQuestionBreakdown()
    {
        using var teacher = await factory.TeacherClientAsync();
        var game = await teacher.CreateSingleChoiceGameAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);
        var start = await client.StartAsync(game.Id);
        await client.SubmitAsync(game.Id, start.Questions[0].Id, new { selectedIndex = 0 }); // Wrong.

        var answers = (await teacher.GetFromJsonAsync<GameAnswersDto>(
            $"/api/admin/games/{game.Id}/answers", Json))!;

        var attempt = Assert.Single(answers.Attempts);
        Assert.Equal(student.DisplayName, attempt.StudentDisplayName);
        var breakdown = Assert.Single(attempt.Answers);
        Assert.Equal(0, breakdown.AutoPoints);
        Assert.Equal(0, breakdown.FinalPoints);
        Assert.False(breakdown.IsOverridden);
        Assert.Contains("correctIndex", breakdown.ContentJson); // Teacher sees the key.
    }

    [Fact]
    public async Task StudentAnswerReview_RequiresFinalizedAttempt_ThenShowsKey()
    {
        using var teacher = await factory.TeacherClientAsync();
        var game = await teacher.CreateSingleChoiceGameAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);

        // In progress -> no review yet.
        var start = await client.StartAsync(game.Id);
        var whileInProgress = await client.GetAsync($"/api/student/games/{game.Id}/answers");
        Assert.Equal(HttpStatusCode.NotFound, whileInProgress.StatusCode);

        await client.SubmitAsync(game.Id, start.Questions[0].Id, new { selectedIndex = 0 });

        var review = (await client.GetFromJsonAsync<MyAnswersDto>(
            $"/api/student/games/{game.Id}/answers", Json))!;
        var breakdown = Assert.Single(review.Answers);
        Assert.Contains("correctIndex", breakdown.ContentJson); // Correct answer visible now.
        Assert.Equal(0, breakdown.FinalPoints);

        // Students cannot see other people's breakdowns via the admin route.
        var adminRoute = await client.GetAsync($"/api/admin/games/{game.Id}/answers");
        Assert.Equal(HttpStatusCode.Forbidden, adminRoute.StatusCode);
    }

    [Fact]
    public async Task StudentReview_ShowsTeacherOverride()
    {
        using var teacher = await factory.TeacherClientAsync();
        var game = await teacher.CreateSingleChoiceGameAsync(xpReward: 10);
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);
        var start = await client.StartAsync(game.Id);
        var result = await client.SubmitAsync(game.Id, start.Questions[0].Id, new { selectedIndex = 0 });

        await teacher.PostAsJsonAsync($"/api/admin/attempts/{result.AttemptId}/override-answer",
            new { questionId = game.Questions.Single().Id, points = 1 }, Json);

        var review = (await client.GetFromJsonAsync<MyAnswersDto>(
            $"/api/student/games/{game.Id}/answers", Json))!;
        var breakdown = Assert.Single(review.Answers);
        Assert.True(breakdown.IsOverridden);
        Assert.Equal(1, breakdown.FinalPoints);
        Assert.Equal(0, breakdown.AutoPoints);
        Assert.Equal(10, review.Result.EarnedXp);
    }

    [Fact]
    public async Task Leaderboard_SplitsClassAndGlobal()
    {
        using var teacher = await factory.TeacherClientAsync();
        var categoryResponse = await teacher.PostAsJsonAsync("/api/admin/categories",
            new { name = Unique("Class") }, Json);
        var category = (await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>(Json))!;

        var inClass = await factory.CreateActivatedStudentAsync(teacher, [category.Id]);
        var outsider = await factory.CreateActivatedStudentAsync(teacher);

        using var inClassClient = await factory.StudentClientAsync(inClass);
        var boards = (await inClassClient.GetFromJsonAsync<LeaderboardResponse>("/api/leaderboard", Json))!;

        var classBoard = Assert.Single(boards.Classes);
        Assert.Equal(category.Name, classBoard.Name);
        Assert.Contains(classBoard.Entries, e => e.DisplayName == inClass.DisplayName);
        Assert.DoesNotContain(classBoard.Entries, e => e.DisplayName == outsider.DisplayName);
        Assert.Contains(boards.GlobalEntries, e => e.DisplayName == inClass.DisplayName);
        Assert.Contains(boards.GlobalEntries, e => e.DisplayName == outsider.DisplayName);

        // A student with no class gets an empty list of class boards.
        using var outsiderClient = await factory.StudentClientAsync(outsider);
        var outsiderBoards = (await outsiderClient.GetFromJsonAsync<LeaderboardResponse>("/api/leaderboard", Json))!;
        Assert.Empty(outsiderBoards.Classes);
    }

    [Fact]
    public async Task Leaderboard_ExcludesDeactivatedStudents()
    {
        using var teacher = await factory.TeacherClientAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);
        var other = await factory.CreateActivatedStudentAsync(teacher);

        await teacher.PostAsync($"/api/admin/students/{student.Id}/deactivate", null);

        using var client = await factory.StudentClientAsync(other);
        var boards = (await client.GetFromJsonAsync<LeaderboardResponse>("/api/leaderboard", Json))!;

        Assert.DoesNotContain(boards.GlobalEntries, e => e.DisplayName == student.DisplayName);
    }

    [Fact]
    public async Task Leaderboard_StudentInTwoClasses_GetsTwoClassBoards()
    {
        using var teacher = await factory.TeacherClientAsync();
        var categoryA = (await (await teacher.PostAsJsonAsync("/api/admin/categories",
            new { name = Unique("Class") }, Json)).Content.ReadFromJsonAsync<CategoryDto>(Json))!;
        var categoryB = (await (await teacher.PostAsJsonAsync("/api/admin/categories",
            new { name = Unique("Class") }, Json)).Content.ReadFromJsonAsync<CategoryDto>(Json))!;

        var student = await factory.CreateActivatedStudentAsync(teacher, [categoryA.Id, categoryB.Id]);

        using var client = await factory.StudentClientAsync(student);
        var boards = (await client.GetFromJsonAsync<LeaderboardResponse>("/api/leaderboard", Json))!;

        Assert.Equal(2, boards.Classes.Count);
        Assert.Contains(boards.Classes, c => c.Id == categoryA.Id);
        Assert.Contains(boards.Classes, c => c.Id == categoryB.Id);
        Assert.All(boards.Classes, c => Assert.Contains(c.Entries, e => e.DisplayName == student.DisplayName));
    }

    [Fact]
    public async Task GamesList_IncludesAttempterDisplayNames()
    {
        using var teacher = await factory.TeacherClientAsync();
        var game = await teacher.CreateSingleChoiceGameAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);
        var start = await client.StartAsync(game.Id);
        await client.SubmitAsync(game.Id, start.Questions[0].Id, new { selectedIndex = 1 });

        var list = (await teacher.GetFromJsonAsync<List<GameSummaryDto>>("/api/admin/games", Json))!;
        var summary = list.Single(g => g.Id == game.Id);

        Assert.Contains(student.DisplayName, summary.AttemptDisplayNames);
    }
}
