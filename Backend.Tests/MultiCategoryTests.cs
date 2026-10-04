using System.Net;
using System.Net.Http.Json;
using Backend.DTOs;
using Backend.Models;
using static Backend.Tests.TestHelpers;

namespace Backend.Tests;

/// <summary>Covers the many-to-many student↔category model: assignment visibility
/// across multiple categories, and the "duplicate assignment" template feature.</summary>
[Collection("Api")]
public class MultiCategoryTests(ApiFactory factory)
{
    private static async Task<CategoryDto> CreateCategoryAsync(HttpClient teacher)
    {
        var response = await teacher.PostAsJsonAsync("/api/admin/categories",
            new { name = Unique("Category") }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryDto>(Json))!;
    }

    [Fact]
    public async Task Student_in_two_categories_sees_assignments_of_both_and_general_but_not_a_third()
    {
        using var teacher = await factory.TeacherClientAsync();
        var categoryA = await CreateCategoryAsync(teacher);
        var categoryB = await CreateCategoryAsync(teacher);
        var categoryC = await CreateCategoryAsync(teacher);

        var assignmentA = await teacher.CreateSingleChoiceAssignmentAsync(categoryId: categoryA.Id);
        var assignmentB = await teacher.CreateSingleChoiceAssignmentAsync(categoryId: categoryB.Id);
        var assignmentC = await teacher.CreateSingleChoiceAssignmentAsync(categoryId: categoryC.Id);
        var assignmentGeneral = await teacher.CreateSingleChoiceAssignmentAsync();

        var student = await factory.CreateActivatedStudentAsync(teacher, [categoryA.Id, categoryB.Id]);
        using var client = await factory.StudentClientAsync(student);

        var dashboard = (await client.GetFromJsonAsync<List<StudentAssignmentSummaryDto>>("/api/student/assignments", Json))!;

        Assert.Contains(dashboard, g => g.Id == assignmentA.Id);
        Assert.Contains(dashboard, g => g.Id == assignmentB.Id);
        Assert.Contains(dashboard, g => g.Id == assignmentGeneral.Id);
        Assert.DoesNotContain(dashboard, g => g.Id == assignmentC.Id);

        var assignmentADto = dashboard.Single(g => g.Id == assignmentA.Id);
        Assert.Equal(categoryA.Id, assignmentADto.CategoryId);
        Assert.Equal(categoryA.Name, assignmentADto.CategoryName);

        var generalDto = dashboard.Single(g => g.Id == assignmentGeneral.Id);
        Assert.Null(generalDto.CategoryId);
        Assert.Null(generalDto.CategoryName);
    }

    [Fact]
    public async Task Duplicate_returns_created_draft_copy_with_same_questions_and_leaves_original_untouched()
    {
        using var teacher = await factory.TeacherClientAsync();
        var original = await teacher.CreateSingleChoiceAssignmentAsync();

        var response = await teacher.PostAsync($"/api/admin/assignments/{original.Id}/duplicate", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var copy = (await response.Content.ReadFromJsonAsync<AssignmentDetailDto>(Json))!;
        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal(original.Title + " (copy)", copy.Title);
        Assert.Equal(AssignmentState.Draft, copy.State);
        Assert.Equal(original.Questions.Count, copy.Questions.Count);
        Assert.Equal(original.Questions[0].JsonContent, copy.Questions[0].JsonContent);
        Assert.Equal(original.Questions[0].Prompt, copy.Questions[0].Prompt);
        Assert.Equal(original.Questions[0].Points, copy.Questions[0].Points);

        // The original is untouched (still Active from CreateSingleChoiceAssignmentAsync).
        var reloadedOriginal = (await teacher.GetFromJsonAsync<AssignmentDetailDto>(
            $"/api/admin/assignments/{original.Id}", Json))!;
        Assert.Equal(AssignmentState.Active, reloadedOriginal.State);
        Assert.Single(reloadedOriginal.Questions);
    }

    [Fact]
    public async Task Duplicate_returns_404_for_another_teachers_assignment()
    {
        using var super = await factory.SuperAdminClientAsync();
        var otherAdmin = await factory.CreateActivatedAdminAsync(super);
        var otherAuth = await factory.LoginAsync(otherAdmin.Username, otherAdmin.Password);
        using var otherClient = factory.ClientWithToken(otherAuth.Token);
        var otherAssignment = await otherClient.CreateSingleChoiceAssignmentAsync();

        using var teacher = await factory.TeacherClientAsync();
        var response = await teacher.PostAsync($"/api/admin/assignments/{otherAssignment.Id}/duplicate", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
