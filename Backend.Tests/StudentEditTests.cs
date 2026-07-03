using System.Net;
using System.Net.Http.Json;
using Backend.DTOs;
using static Backend.Tests.TestHelpers;

namespace Backend.Tests;

[Collection("Api")]
public class StudentEditTests(ApiFactory factory)
{
    private static async Task<CategoryDto> CreateCategoryAsync(HttpClient admin)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/categories",
            new { name = Unique("Class") }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryDto>(Json))!;
    }

    [Fact]
    public async Task Update_edits_pii_and_assigns_own_category_then_unassigns()
    {
        using var teacher = await factory.TeacherClientAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);
        var category = await CreateCategoryAsync(teacher);

        var updated = await teacher.PutAsJsonAsync($"/api/admin/students/{student.Id}", new
        {
            firstName = "New",
            lastName = "Name",
            email = "new.email@test.local",
            displayName = $"R-{student.Username}",
            categoryIds = new[] { category.Id }
        }, Json);
        updated.EnsureSuccessStatusCode();
        var dto = (await updated.Content.ReadFromJsonAsync<StudentAdminDto>(Json))!;
        Assert.Equal("New", dto.FirstName);
        Assert.Equal("new.email@test.local", dto.Email);
        Assert.Equal($"R-{student.Username}", dto.DisplayName);
        var cat = Assert.Single(dto.Categories);
        Assert.Equal(category.Id, cat.Id);
        Assert.Equal(category.Name, cat.Name);

        // empty categoryIds unassigns.
        var unassigned = await teacher.PutAsJsonAsync($"/api/admin/students/{student.Id}", new
        {
            firstName = "New",
            lastName = "Name",
            email = "new.email@test.local",
            displayName = (string?)null,
            categoryIds = Array.Empty<int>()
        }, Json);
        unassigned.EnsureSuccessStatusCode();
        var dto2 = (await unassigned.Content.ReadFromJsonAsync<StudentAdminDto>(Json))!;
        Assert.Empty(dto2.Categories);
    }

    [Fact]
    public async Task Update_with_multiple_categoryIds_assigns_both_then_clears()
    {
        using var teacher = await factory.TeacherClientAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);
        var categoryA = await CreateCategoryAsync(teacher);
        var categoryB = await CreateCategoryAsync(teacher);

        var updated = await teacher.PutAsJsonAsync($"/api/admin/students/{student.Id}", new
        {
            firstName = "Multi",
            lastName = "Class",
            email = (string?)null,
            displayName = (string?)null,
            categoryIds = new[] { categoryA.Id, categoryB.Id }
        }, Json);
        updated.EnsureSuccessStatusCode();
        var dto = (await updated.Content.ReadFromJsonAsync<StudentAdminDto>(Json))!;
        Assert.Equal(2, dto.Categories.Count);
        Assert.Contains(dto.Categories, c => c.Id == categoryA.Id);
        Assert.Contains(dto.Categories, c => c.Id == categoryB.Id);

        var cleared = await teacher.PutAsJsonAsync($"/api/admin/students/{student.Id}", new
        {
            firstName = "Multi",
            lastName = "Class",
            email = (string?)null,
            displayName = (string?)null,
            categoryIds = (int[]?)null
        }, Json);
        cleared.EnsureSuccessStatusCode();
        var dto2 = (await cleared.Content.ReadFromJsonAsync<StudentAdminDto>(Json))!;
        Assert.Empty(dto2.Categories);
    }

    [Fact]
    public async Task Admin_cannot_assign_another_admins_category()
    {
        using var super = await factory.SuperAdminClientAsync();
        var otherAdmin = await factory.CreateActivatedAdminAsync(super);
        var otherAuth = await factory.LoginAsync(otherAdmin.Username, otherAdmin.Password);
        using var otherClient = factory.ClientWithToken(otherAuth.Token);
        var foreignCategory = await CreateCategoryAsync(otherClient);

        using var teacher = await factory.TeacherClientAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);

        var response = await teacher.PutAsJsonAsync($"/api/admin/students/{student.Id}", new
        {
            firstName = "X",
            lastName = "Y",
            email = (string?)null,
            displayName = (string?)null,
            categoryIds = new[] { foreignCategory.Id }
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_can_assign_any_admins_category()
    {
        using var teacher = await factory.TeacherClientAsync();
        var category = await CreateCategoryAsync(teacher);
        var student = await factory.CreateActivatedStudentAsync(teacher);

        using var super = await factory.SuperAdminClientAsync();
        var response = await super.PutAsJsonAsync($"/api/admin/students/{student.Id}", new
        {
            firstName = "Test",
            lastName = "Student",
            email = (string?)null,
            displayName = (string?)null,
            categoryIds = new[] { category.Id }
        }, Json);
        response.EnsureSuccessStatusCode();
        var dto = (await response.Content.ReadFromJsonAsync<StudentAdminDto>(Json))!;
        Assert.Contains(dto.Categories, c => c.Id == category.Id);
    }

    [Fact]
    public async Task Unknown_category_returns_bad_request()
    {
        using var teacher = await factory.TeacherClientAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);

        var response = await teacher.PutAsJsonAsync($"/api/admin/students/{student.Id}", new
        {
            firstName = "X",
            lastName = "Y",
            email = (string?)null,
            displayName = (string?)null,
            categoryIds = new[] { 999999 }
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_display_name_returns_conflict()
    {
        using var teacher = await factory.TeacherClientAsync();
        var first = await factory.CreateActivatedStudentAsync(teacher);
        var second = await factory.CreateActivatedStudentAsync(teacher);

        var response = await teacher.PutAsJsonAsync($"/api/admin/students/{second.Id}", new
        {
            firstName = "X",
            lastName = "Y",
            email = (string?)null,
            displayName = first.DisplayName,
            categoryIds = (int[]?)null
        }, Json);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_sees_all_categories_admin_sees_own()
    {
        using var teacher = await factory.TeacherClientAsync();
        var own = await CreateCategoryAsync(teacher);

        using var super = await factory.SuperAdminClientAsync();
        var otherAdmin = await factory.CreateActivatedAdminAsync(super);
        var otherAuth = await factory.LoginAsync(otherAdmin.Username, otherAdmin.Password);
        using var otherClient = factory.ClientWithToken(otherAuth.Token);
        var foreign = await CreateCategoryAsync(otherClient);

        var teacherList = await teacher.GetFromJsonAsync<List<CategoryDto>>("/api/admin/categories", Json);
        Assert.Contains(teacherList!, c => c.Id == own.Id);
        Assert.DoesNotContain(teacherList!, c => c.Id == foreign.Id);

        var superList = await super.GetFromJsonAsync<List<CategoryDto>>("/api/admin/categories", Json);
        Assert.Contains(superList!, c => c.Id == own.Id);
        Assert.Contains(superList!, c => c.Id == foreign.Id);
    }
}
