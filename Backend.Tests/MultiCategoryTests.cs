using System.Net;
using System.Net.Http.Json;
using Backend.DTOs;
using Backend.Models;
using static Backend.Tests.TestHelpers;

namespace Backend.Tests;

/// <summary>Covers the many-to-many student↔category model: game visibility
/// across multiple classes, and the "duplicate game" template feature.</summary>
[Collection("Api")]
public class MultiCategoryTests(ApiFactory factory)
{
    private static async Task<CategoryDto> CreateCategoryAsync(HttpClient teacher)
    {
        var response = await teacher.PostAsJsonAsync("/api/admin/categories",
            new { name = Unique("Class") }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryDto>(Json))!;
    }

    [Fact]
    public async Task Student_in_two_categories_sees_games_of_both_and_general_but_not_a_third()
    {
        using var teacher = await factory.TeacherClientAsync();
        var categoryA = await CreateCategoryAsync(teacher);
        var categoryB = await CreateCategoryAsync(teacher);
        var categoryC = await CreateCategoryAsync(teacher);

        var gameA = await teacher.CreateSingleChoiceGameAsync(categoryId: categoryA.Id);
        var gameB = await teacher.CreateSingleChoiceGameAsync(categoryId: categoryB.Id);
        var gameC = await teacher.CreateSingleChoiceGameAsync(categoryId: categoryC.Id);
        var gameGeneral = await teacher.CreateSingleChoiceGameAsync();

        var student = await factory.CreateActivatedStudentAsync(teacher, [categoryA.Id, categoryB.Id]);
        using var client = await factory.StudentClientAsync(student);

        var dashboard = (await client.GetFromJsonAsync<List<StudentGameSummaryDto>>("/api/student/games", Json))!;

        Assert.Contains(dashboard, g => g.Id == gameA.Id);
        Assert.Contains(dashboard, g => g.Id == gameB.Id);
        Assert.Contains(dashboard, g => g.Id == gameGeneral.Id);
        Assert.DoesNotContain(dashboard, g => g.Id == gameC.Id);

        var gameADto = dashboard.Single(g => g.Id == gameA.Id);
        Assert.Equal(categoryA.Id, gameADto.CategoryId);
        Assert.Equal(categoryA.Name, gameADto.CategoryName);

        var generalDto = dashboard.Single(g => g.Id == gameGeneral.Id);
        Assert.Null(generalDto.CategoryId);
        Assert.Null(generalDto.CategoryName);
    }

    [Fact]
    public async Task Duplicate_returns_created_draft_copy_with_same_questions_and_leaves_original_untouched()
    {
        using var teacher = await factory.TeacherClientAsync();
        var original = await teacher.CreateSingleChoiceGameAsync();

        var response = await teacher.PostAsync($"/api/admin/games/{original.Id}/duplicate", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var copy = (await response.Content.ReadFromJsonAsync<GameDetailDto>(Json))!;
        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal(original.Title + " (copy)", copy.Title);
        Assert.Equal(GameState.Draft, copy.State);
        Assert.Equal(original.Questions.Count, copy.Questions.Count);
        Assert.Equal(original.Questions[0].JsonContent, copy.Questions[0].JsonContent);
        Assert.Equal(original.Questions[0].Prompt, copy.Questions[0].Prompt);
        Assert.Equal(original.Questions[0].Points, copy.Questions[0].Points);

        // The original is untouched (still Active from CreateSingleChoiceGameAsync).
        var reloadedOriginal = (await teacher.GetFromJsonAsync<GameDetailDto>(
            $"/api/admin/games/{original.Id}", Json))!;
        Assert.Equal(GameState.Active, reloadedOriginal.State);
        Assert.Single(reloadedOriginal.Questions);
    }

    [Fact]
    public async Task Duplicate_returns_404_for_another_teachers_game()
    {
        using var super = await factory.SuperAdminClientAsync();
        var otherAdmin = await factory.CreateActivatedAdminAsync(super);
        var otherAuth = await factory.LoginAsync(otherAdmin.Username, otherAdmin.Password);
        using var otherClient = factory.ClientWithToken(otherAuth.Token);
        var otherGame = await otherClient.CreateSingleChoiceGameAsync();

        using var teacher = await factory.TeacherClientAsync();
        var response = await teacher.PostAsync($"/api/admin/games/{otherGame.Id}/duplicate", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
