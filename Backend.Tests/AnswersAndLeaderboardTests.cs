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
        var assignment = await teacher.CreateSingleChoiceAssignmentAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);
        var start = await client.StartAsync(assignment.Id);
        await client.SubmitAsync(assignment.Id, start.Questions[0].Id, new { selectedIndex = 0 }); // Wrong.

        var answers = (await teacher.GetFromJsonAsync<AssignmentAnswersDto>(
            $"/api/admin/assignments/{assignment.Id}/answers", Json))!;

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
        var assignment = await teacher.CreateSingleChoiceAssignmentAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);

        // In progress -> no review yet.
        var start = await client.StartAsync(assignment.Id);
        var whileInProgress = await client.GetAsync($"/api/student/assignments/{assignment.Id}/answers");
        Assert.Equal(HttpStatusCode.NotFound, whileInProgress.StatusCode);

        await client.SubmitAsync(assignment.Id, start.Questions[0].Id, new { selectedIndex = 0 });

        var review = (await client.GetFromJsonAsync<MyAnswersDto>(
            $"/api/student/assignments/{assignment.Id}/answers", Json))!;
        var breakdown = Assert.Single(review.Answers);
        Assert.Contains("correctIndex", breakdown.ContentJson); // Correct answer visible now.
        Assert.Equal(0, breakdown.FinalPoints);

        // Students cannot see other people's breakdowns via the admin route.
        var adminRoute = await client.GetAsync($"/api/admin/assignments/{assignment.Id}/answers");
        Assert.Equal(HttpStatusCode.Forbidden, adminRoute.StatusCode);
    }

    [Fact]
    public async Task StudentReview_ShowsTeacherOverride()
    {
        using var teacher = await factory.TeacherClientAsync();
        var assignment = await teacher.CreateSingleChoiceAssignmentAsync(xpReward: 10);
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);
        var start = await client.StartAsync(assignment.Id);
        var result = await client.SubmitAsync(assignment.Id, start.Questions[0].Id, new { selectedIndex = 0 });

        await teacher.PostAsJsonAsync($"/api/admin/attempts/{result.AttemptId}/override-answer",
            new { questionId = assignment.Questions.Single().Id, points = 1 }, Json);

        var review = (await client.GetFromJsonAsync<MyAnswersDto>(
            $"/api/student/assignments/{assignment.Id}/answers", Json))!;
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
            new { name = Unique("Category") }, Json);
        var category = (await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>(Json))!;

        var inCategory = await factory.CreateActivatedStudentAsync(teacher, [category.Id]);
        var outsider = await factory.CreateActivatedStudentAsync(teacher);

        using var inCategoryClient = await factory.StudentClientAsync(inCategory);
        var boards = (await inCategoryClient.GetFromJsonAsync<LeaderboardResponse>("/api/leaderboard", Json))!;

        var categoryBoard = Assert.Single(boards.Categories);
        Assert.Equal(category.Name, categoryBoard.Name);
        Assert.Contains(categoryBoard.Entries, e => e.DisplayName == inCategory.DisplayName);
        Assert.DoesNotContain(categoryBoard.Entries, e => e.DisplayName == outsider.DisplayName);
        Assert.Contains(boards.GlobalEntries, e => e.DisplayName == inCategory.DisplayName);
        Assert.Contains(boards.GlobalEntries, e => e.DisplayName == outsider.DisplayName);

        // A student with no category gets an empty list of category boards.
        using var outsiderClient = await factory.StudentClientAsync(outsider);
        var outsiderBoards = (await outsiderClient.GetFromJsonAsync<LeaderboardResponse>("/api/leaderboard", Json))!;
        Assert.Empty(outsiderBoards.Categories);
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
            new { name = Unique("Category") }, Json)).Content.ReadFromJsonAsync<CategoryDto>(Json))!;
        var categoryB = (await (await teacher.PostAsJsonAsync("/api/admin/categories",
            new { name = Unique("Category") }, Json)).Content.ReadFromJsonAsync<CategoryDto>(Json))!;

        var student = await factory.CreateActivatedStudentAsync(teacher, [categoryA.Id, categoryB.Id]);

        using var client = await factory.StudentClientAsync(student);
        var boards = (await client.GetFromJsonAsync<LeaderboardResponse>("/api/leaderboard", Json))!;

        Assert.Equal(2, boards.Categories.Count);
        Assert.Contains(boards.Categories, c => c.Id == categoryA.Id);
        Assert.Contains(boards.Categories, c => c.Id == categoryB.Id);
        Assert.All(boards.Categories, c => Assert.Contains(c.Entries, e => e.DisplayName == student.DisplayName));
    }

    [Fact]
    public async Task AssignmentsList_IncludesAttempterDisplayNames()
    {
        using var teacher = await factory.TeacherClientAsync();
        var assignment = await teacher.CreateSingleChoiceAssignmentAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);
        var start = await client.StartAsync(assignment.Id);
        await client.SubmitAsync(assignment.Id, start.Questions[0].Id, new { selectedIndex = 1 });

        var list = (await teacher.GetFromJsonAsync<List<AssignmentSummaryDto>>("/api/admin/assignments", Json))!;
        var summary = list.Single(g => g.Id == assignment.Id);

        Assert.Contains(student.DisplayName, summary.AttemptDisplayNames);
    }
}
