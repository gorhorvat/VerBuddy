using System.Net;
using System.Net.Http.Json;
using Backend.DTOs;
using static Backend.Tests.TestHelpers;

namespace Backend.Tests;

[Collection("Api")]
public class AuthAndGdprTests(ApiFactory factory)
{
    [Fact]
    public async Task TeacherLogin_ReturnsTokenAndRole()
    {
        var auth = await factory.LoginAsync(TeacherUsername, TeacherPassword);

        Assert.NotEmpty(auth.Token);
        Assert.Contains("Teacher", auth.Roles);
        Assert.False(auth.MustChangePassword);
    }

    [Fact]
    public async Task WrongPassword_Returns401()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { username = TeacherUsername, password = "wrong-password" }, Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StudentToken_CannotAccessAdminEndpoints()
    {
        using var teacher = await factory.TeacherClientAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);

        foreach (var path in new[] { "/api/admin/games", "/api/admin/students", "/api/admin/categories", "/api/admin/attempts" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task Anonymous_CannotAccessProtectedEndpoints()
    {
        using var client = factory.CreateClient();
        foreach (var path in new[] { "/api/admin/games", "/api/student/games", "/api/leaderboard", "/api/auth/me" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Leaderboard_NeverContainsPii()
    {
        using var teacher = await factory.TeacherClientAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);

        var raw = await client.GetStringAsync("/api/leaderboard");

        Assert.Contains(student.DisplayName, raw);
        Assert.DoesNotContain("firstName", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lastName", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@test.local", raw);
    }

    [Fact]
    public async Task StudentGamePayload_DoesNotContainAnswerKey()
    {
        using var teacher = await factory.TeacherClientAsync();
        var game = await teacher.CreateSingleChoiceGameAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);

        var response = await client.PostAsync($"/api/student/games/{game.Id}/start", null);
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Contains("choices", raw);
        Assert.DoesNotContain("correctIndex", raw);
    }

    [Fact]
    public async Task Me_ReturnsOnlyPseudonymousFields()
    {
        using var teacher = await factory.TeacherClientAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);

        var raw = await client.GetStringAsync("/api/auth/me");

        Assert.Contains(student.DisplayName, raw);
        Assert.DoesNotContain("firstName", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", raw, StringComparison.OrdinalIgnoreCase);
    }
}
