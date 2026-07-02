using System.Net;
using System.Net.Http.Json;
using Backend.DTOs;
using static Backend.Tests.TestHelpers;

namespace Backend.Tests;

[Collection("Api")]
public class GameLifecycleTests(ApiFactory factory)
{
    [Fact]
    public async Task Activate_WithoutQuestions_Returns409()
    {
        using var teacher = await factory.TeacherClientAsync();
        var create = await teacher.PostAsJsonAsync("/api/admin/games",
            new { title = Unique("Empty"), gameType = "SingleChoice" }, Json);
        var game = (await create.Content.ReadFromJsonAsync<GameDetailDto>(Json))!;

        var response = await teacher.PostAsJsonAsync($"/api/admin/games/{game.Id}/state", new { state = "Active" }, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task InvalidQuestionContent_Returns400()
    {
        using var teacher = await factory.TeacherClientAsync();
        var game = await teacher.CreateSingleChoiceGameAsync(activate: false);

        var response = await teacher.PostAsJsonAsync($"/api/admin/games/{game.Id}/questions", new
        {
            prompt = "Broken",
            order = 2,
            points = 1,
            jsonContent = """{"choices":["A","B"],"correctIndex":9}"""
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Questions_FrozenWhileActive_EditableWhenClosed()
    {
        using var teacher = await factory.TeacherClientAsync();
        var game = await teacher.CreateSingleChoiceGameAsync(); // Active
        var question = game.Questions.Single();
        var edit = new
        {
            prompt = "Pick B. (revised)",
            order = question.Order,
            points = question.Points,
            jsonContent = question.JsonContent
        };

        var whileActive = await teacher.PutAsJsonAsync(
            $"/api/admin/games/{game.Id}/questions/{question.Id}", edit, Json);
        Assert.Equal(HttpStatusCode.Conflict, whileActive.StatusCode);

        await teacher.PostAsJsonAsync($"/api/admin/games/{game.Id}/state", new { state = "Closed" }, Json);
        var whenClosed = await teacher.PutAsJsonAsync(
            $"/api/admin/games/{game.Id}/questions/{question.Id}", edit, Json);
        Assert.Equal(HttpStatusCode.OK, whenClosed.StatusCode);

        var updated = (await whenClosed.Content.ReadFromJsonAsync<QuestionAdminDto>(Json))!;
        Assert.Equal("Pick B. (revised)", updated.Prompt);
    }

    [Fact]
    public async Task TimerAndGradingMode_FrozenOnlyWhileActive()
    {
        using var teacher = await factory.TeacherClientAsync();
        var game = await teacher.CreateSingleChoiceGameAsync(timeLimitSeconds: 60); // Active

        var whileActive = await teacher.PutAsJsonAsync($"/api/admin/games/{game.Id}", new
        {
            title = game.Title,
            description = (string?)null,
            timeLimitSeconds = 120,
            xpReward = game.XpReward,
            requireFeedback = game.RequireFeedback,
            categoryId = (int?)null
        }, Json);
        Assert.Equal(HttpStatusCode.Conflict, whileActive.StatusCode);

        await teacher.PostAsJsonAsync($"/api/admin/games/{game.Id}/state", new { state = "Closed" }, Json);
        var whenClosed = await teacher.PutAsJsonAsync($"/api/admin/games/{game.Id}", new
        {
            title = game.Title,
            description = (string?)null,
            timeLimitSeconds = 120,
            xpReward = game.XpReward,
            requireFeedback = game.RequireFeedback,
            categoryId = (int?)null
        }, Json);
        Assert.Equal(HttpStatusCode.OK, whenClosed.StatusCode);
    }

    [Fact]
    public async Task DeleteGame_WithAttempts_Returns409()
    {
        using var teacher = await factory.TeacherClientAsync();
        var game = await teacher.CreateSingleChoiceGameAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var studentClient = await factory.StudentClientAsync(student);
        var start = await studentClient.StartAsync(game.Id);
        await studentClient.SubmitAsync(game.Id, start.Questions[0].Id, new { selectedIndex = 1 });

        var response = await teacher.DeleteAsync($"/api/admin/games/{game.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Categories_CrudAndAssignment()
    {
        using var teacher = await factory.TeacherClientAsync();
        var name = Unique("Class");

        var create = await teacher.PostAsJsonAsync("/api/admin/categories", new { name }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var category = (await create.Content.ReadFromJsonAsync<CategoryDto>(Json))!;

        // Duplicate name rejected.
        var duplicate = await teacher.PostAsJsonAsync("/api/admin/categories", new { name }, Json);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        // Assign an ACTIVE game — category moves are allowed in any state.
        var game = await teacher.CreateSingleChoiceGameAsync();
        var assign = await teacher.PostAsJsonAsync(
            $"/api/admin/games/{game.Id}/category", new { categoryId = category.Id }, Json);
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        var assigned = (await assign.Content.ReadFromJsonAsync<GameDetailDto>(Json))!;
        Assert.Equal(name, assigned.CategoryName);

        // Deleting the category files the game back under General (null).
        var delete = await teacher.DeleteAsync($"/api/admin/categories/{category.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var after = (await teacher.GetFromJsonAsync<GameDetailDto>($"/api/admin/games/{game.Id}", Json))!;
        Assert.Null(after.CategoryId);
    }

    [Fact]
    public async Task ActiveToDraft_WithAttempts_Returns409()
    {
        using var teacher = await factory.TeacherClientAsync();
        var game = await teacher.CreateSingleChoiceGameAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var studentClient = await factory.StudentClientAsync(student);
        var start = await studentClient.StartAsync(game.Id);
        await studentClient.SubmitAsync(game.Id, start.Questions[0].Id, new { selectedIndex = 1 });

        var response = await teacher.PostAsJsonAsync(
            $"/api/admin/games/{game.Id}/state", new { state = "Draft" }, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
