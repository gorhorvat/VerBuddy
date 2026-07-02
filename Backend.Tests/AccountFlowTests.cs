using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Backend.DTOs;
using static Backend.Tests.TestHelpers;

namespace Backend.Tests;

[Collection("Api")]
public class AccountFlowTests(ApiFactory factory)
{
    [Fact]
    public async Task FullActivationFlow_CreateActivateFirstLoginChangePassword()
    {
        using var teacher = await factory.TeacherClientAsync();
        var username = Unique("stu");
        var email = $"{username}@test.local";

        // Create: no password anywhere in the request.
        var create = await teacher.PostAsJsonAsync("/api/admin/students",
            new { username, firstName = "New", lastName = "Kid", email }, Json);
        create.EnsureSuccessStatusCode();
        var dto = (await create.Content.ReadFromJsonAsync<StudentAdminDto>(Json))!;
        Assert.Null(dto.ActivatedAt);

        // Not activated yet -> login impossible (no password on the account).
        using var anon = factory.CreateClient();
        var beforeActivation = await anon.PostAsJsonAsync("/api/auth/login",
            new { username, password = "anything" }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, beforeActivation.StatusCode);

        // Activate -> email with temp password, MustChangePassword set.
        var activate = await teacher.PostAsync($"/api/admin/students/{dto.Id}/activate", null);
        activate.EnsureSuccessStatusCode();
        var activated = (await activate.Content.ReadFromJsonAsync<StudentAdminDto>(Json))!;
        Assert.NotNull(activated.ActivatedAt);
        Assert.True(activated.MustChangePassword);

        var mail = factory.Emails.LastTo(email);
        Assert.Contains(username, mail.Body);
        var tempPassword = ExtractPassword(mail.Body);
        Assert.NotEmpty(tempPassword);

        // First login flags the forced change; change-password clears it.
        var firstLogin = await factory.LoginAsync(username, tempPassword);
        Assert.True(firstLogin.MustChangePassword);

        using var studentClient = factory.ClientWithToken(firstLogin.Token);
        var change = await studentClient.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = tempPassword, newPassword = "MyOwn!Pass123" }, Json);
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        var secondLogin = await factory.LoginAsync(username, "MyOwn!Pass123");
        Assert.False(secondLogin.MustChangePassword);
    }

    [Fact]
    public async Task Activate_WithoutEmail_Returns400()
    {
        using var teacher = await factory.TeacherClientAsync();
        var create = await teacher.PostAsJsonAsync("/api/admin/students",
            new { username = Unique("noemail") }, Json);
        var dto = (await create.Content.ReadFromJsonAsync<StudentAdminDto>(Json))!;

        var activate = await teacher.PostAsync($"/api/admin/students/{dto.Id}/activate", null);

        Assert.Equal(HttpStatusCode.BadRequest, activate.StatusCode);
    }

    [Fact]
    public async Task ResetPasswordFlow_ViaEmailedLink()
    {
        using var teacher = await factory.TeacherClientAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);
        var email = $"{student.Username}@test.local";

        var trigger = await teacher.PostAsync($"/api/admin/students/{student.Id}/reset-password", null);
        Assert.Equal(HttpStatusCode.NoContent, trigger.StatusCode);

        // Parse user + token out of the emailed link.
        var body = factory.Emails.LastTo(email).Body;
        var match = Regex.Match(body, @"reset-password\?user=(?<user>[^&\s]+)&token=(?<token>\S+)");
        Assert.True(match.Success);
        var userId = Uri.UnescapeDataString(match.Groups["user"].Value);
        var token = Uri.UnescapeDataString(match.Groups["token"].Value);

        using var anon = factory.CreateClient();
        var reset = await anon.PostAsJsonAsync("/api/auth/reset-password",
            new { userId, token, newPassword = "AfterReset!123" }, Json);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var login = await factory.LoginAsync(student.Username, "AfterReset!123");
        Assert.False(login.MustChangePassword);

        // The token is single-use.
        var replay = await anon.PostAsJsonAsync("/api/auth/reset-password",
            new { userId, token, newPassword = "Another!123" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    [Fact]
    public async Task Deactivate_BlocksLogin_ReactivateRestoresIt()
    {
        using var teacher = await factory.TeacherClientAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);

        await teacher.PostAsync($"/api/admin/students/{student.Id}/deactivate", null);
        using var anon = factory.CreateClient();
        var blocked = await anon.PostAsJsonAsync("/api/auth/login",
            new { username = student.Username, password = student.Password }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);

        await teacher.PostAsync($"/api/admin/students/{student.Id}/reactivate", null);
        var restored = await factory.LoginAsync(student.Username, student.Password);
        Assert.NotEmpty(restored.Token);
    }

    [Fact]
    public async Task Delete_FreshAccountSucceeds_WithAttemptsRefused()
    {
        using var teacher = await factory.TeacherClientAsync();

        // Fresh account with no attempts: hard delete allowed.
        var fresh = await factory.CreateActivatedStudentAsync(teacher);
        var deleteFresh = await teacher.DeleteAsync($"/api/admin/students/{fresh.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteFresh.StatusCode);

        // Account with history: refused, deactivation is the path.
        var veteran = await factory.CreateActivatedStudentAsync(teacher);
        var game = await teacher.CreateSingleChoiceGameAsync();
        using var client = await factory.StudentClientAsync(veteran);
        var start = await client.StartAsync(game.Id);
        await client.SubmitAsync(game.Id, start.Questions[0].Id, new { selectedIndex = 1 });

        var deleteVeteran = await teacher.DeleteAsync($"/api/admin/students/{veteran.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deleteVeteran.StatusCode);
    }

    [Fact]
    public async Task Import_CreatesRows_AndBulkActivateReportsErrors()
    {
        using var teacher = await factory.TeacherClientAsync();
        var withEmail = Unique("csv1");
        var withoutEmail = Unique("csv2");

        var import = await teacher.PostAsJsonAsync("/api/admin/students/import", new
        {
            rows = new object[]
            {
                new { username = withEmail, firstName = "Csv", lastName = "One", email = $"{withEmail}@test.local", displayName = (string?)null },
                new { username = withoutEmail, firstName = "Csv", lastName = "Two", email = (string?)null, displayName = (string?)null }
            },
            categoryId = (int?)null
        }, Json);
        import.EnsureSuccessStatusCode();
        var result = (await import.Content.ReadFromJsonAsync<ImportStudentsResult>(Json))!;
        Assert.Equal(2, result.Created.Count);
        Assert.Empty(result.Errors);

        // Bulk activation: the one without an email is reported, not fatal.
        var ids = result.Created.Select(s => s.Id).ToList();
        var bulk = await teacher.PostAsJsonAsync("/api/admin/students/activate-bulk",
            new { studentIds = ids }, Json);
        bulk.EnsureSuccessStatusCode();
        var bulkResult = (await bulk.Content.ReadFromJsonAsync<BulkActivateResult>(Json))!;

        Assert.Equal(1, bulkResult.Activated);
        Assert.Single(bulkResult.Errors);
        Assert.Contains(withoutEmail, bulkResult.Errors[0]);

        // The activated one received a usable temp password.
        var tempPassword = ExtractPassword(factory.Emails.LastTo($"{withEmail}@test.local").Body);
        var login = await factory.LoginAsync(withEmail, tempPassword);
        Assert.True(login.MustChangePassword);
    }
}
