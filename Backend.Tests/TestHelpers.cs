using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Backend.DTOs;
using Backend.Models;

namespace Backend.Tests;

/// <summary>An activated student ready to log in with a known password.</summary>
public sealed record TestStudent(string Id, string Username, string Password, string DisplayName);

/// <summary>An activated admin ready to log in with a known password.</summary>
public sealed record TestAdmin(string Id, string Username, string Password);

public static class TestHelpers
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public const string TeacherUsername = "teacher.anna";
    public const string TeacherPassword = "ChangeMe!123";

    public const string SuperAdminUsername = "superadmin";
    public const string SuperAdminPassword = "Super!Pass123";

    public static string Unique(string prefix) =>
        $"{prefix}.{Guid.NewGuid():N}"[..Math.Min(30, prefix.Length + 25)];

    // ─── Auth ──────────────────────────────────────────────────────────────

    public static async Task<AuthResponse> LoginAsync(this ApiFactory factory, string username, string password)
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
    }

    public static async Task<HttpClient> TeacherClientAsync(this ApiFactory factory)
    {
        var auth = await factory.LoginAsync(TeacherUsername, TeacherPassword);
        return factory.ClientWithToken(auth.Token);
    }

    public static async Task<HttpClient> SuperAdminClientAsync(this ApiFactory factory)
    {
        var auth = await factory.LoginAsync(SuperAdminUsername, SuperAdminPassword);
        return factory.ClientWithToken(auth.Token);
    }

    public static HttpClient ClientWithToken(this ApiFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ─── Students ──────────────────────────────────────────────────────────

    /// <summary>
    /// Full account flow: create (no password) → activate (email captured) →
    /// first login with the temp password → change to a known password.
    /// </summary>
    public static async Task<TestStudent> CreateActivatedStudentAsync(
        this ApiFactory factory, HttpClient teacher, List<int>? categoryIds = null)
    {
        var username = Unique("stu");
        var email = $"{username}@test.local";

        var createResponse = await teacher.PostAsJsonAsync("/api/admin/students", new
        {
            username,
            firstName = "Test",
            lastName = "Student",
            email,
            displayName = (string?)null,
            categoryIds
        }, Json);
        createResponse.EnsureSuccessStatusCode();
        var dto = (await createResponse.Content.ReadFromJsonAsync<StudentAdminDto>(Json))!;

        var activateResponse = await teacher.PostAsync($"/api/admin/students/{dto.Id}/activate", null);
        activateResponse.EnsureSuccessStatusCode();

        var tempPassword = ExtractPassword(factory.Emails.LastTo(email).Body);

        var firstLogin = await factory.LoginAsync(username, tempPassword);
        Assert.True(firstLogin.MustChangePassword);

        const string finalPassword = "Chosen!Pass123";
        using var studentClient = factory.ClientWithToken(firstLogin.Token);
        var change = await studentClient.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = tempPassword, newPassword = finalPassword }, Json);
        change.EnsureSuccessStatusCode();

        return new TestStudent(dto.Id, username, finalPassword, dto.DisplayName);
    }

    public static async Task<HttpClient> StudentClientAsync(this ApiFactory factory, TestStudent student)
    {
        var auth = await factory.LoginAsync(student.Username, student.Password);
        return factory.ClientWithToken(auth.Token);
    }

    public static async Task<HttpClient> AdminClientAsync(this ApiFactory factory, TestAdmin admin)
    {
        var auth = await factory.LoginAsync(admin.Username, admin.Password);
        return factory.ClientWithToken(auth.Token);
    }

    /// <summary>
    /// Admin account flow: create (no password) → activate (email captured) →
    /// first login with the temp password → change to a known password.
    /// </summary>
    public static async Task<TestAdmin> CreateActivatedAdminAsync(
        this ApiFactory factory, HttpClient superAdmin)
    {
        var username = Unique("adm");
        var email = $"{username}@test.local";

        var createResponse = await superAdmin.PostAsJsonAsync("/api/superadmin/admins", new
        {
            username,
            firstName = "Test",
            lastName = "Admin",
            email,
            displayName = (string?)null
        }, Json);
        createResponse.EnsureSuccessStatusCode();
        var dto = (await createResponse.Content.ReadFromJsonAsync<AdminAccountDto>(Json))!;

        var activateResponse = await superAdmin.PostAsync($"/api/superadmin/admins/{dto.Id}/activate", null);
        activateResponse.EnsureSuccessStatusCode();

        var tempPassword = ExtractPassword(factory.Emails.LastTo(email).Body);

        var firstLogin = await factory.LoginAsync(username, tempPassword);
        Assert.True(firstLogin.MustChangePassword);

        const string finalPassword = "Chosen!Admin123";
        using var client = factory.ClientWithToken(firstLogin.Token);
        var change = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = tempPassword, newPassword = finalPassword }, Json);
        change.EnsureSuccessStatusCode();

        return new TestAdmin(dto.Id, username, finalPassword);
    }

    public static string ExtractPassword(string emailBody) =>
        Regex.Match(emailBody, @"Password:\s*(\S+)").Groups[1].Value;

    // ─── Games ─────────────────────────────────────────────────────────────

    /// <summary>Creates a game with one SingleChoice question ("A"/"B", B correct, 1 pt).</summary>
    public static async Task<GameDetailDto> CreateSingleChoiceGameAsync(
        this HttpClient teacher,
        int? timeLimitSeconds = null,
        int xpReward = 100,
        bool requireFeedback = false,
        int? categoryId = null,
        bool activate = true)
    {
        var create = await teacher.PostAsJsonAsync("/api/admin/games", new
        {
            title = Unique("Game"),
            gameType = "SingleChoice",
            timeLimitSeconds,
            xpReward,
            requireFeedback,
            categoryId
        }, Json);
        create.EnsureSuccessStatusCode();
        var game = (await create.Content.ReadFromJsonAsync<GameDetailDto>(Json))!;

        var question = await teacher.PostAsJsonAsync($"/api/admin/games/{game.Id}/questions", new
        {
            prompt = "Pick B.",
            order = 1,
            points = 1,
            jsonContent = """{"choices":["A","B"],"correctIndex":1}"""
        }, Json);
        question.EnsureSuccessStatusCode();

        if (activate)
        {
            var state = await teacher.PostAsJsonAsync($"/api/admin/games/{game.Id}/state", new { state = "Active" }, Json);
            state.EnsureSuccessStatusCode();
        }

        return (await teacher.GetFromJsonAsync<GameDetailDto>($"/api/admin/games/{game.Id}", Json))!;
    }

    /// <summary>Creates an Active WordMatching game: one question, 4 pairs, 4 points.</summary>
    public static async Task<GameDetailDto> CreateWordMatchingGameAsync(
        this HttpClient teacher, int xpReward = 100)
    {
        var create = await teacher.PostAsJsonAsync("/api/admin/games", new
        {
            title = Unique("Match"),
            gameType = "WordMatching",
            timeLimitSeconds = (int?)null,
            xpReward
        }, Json);
        create.EnsureSuccessStatusCode();
        var game = (await create.Content.ReadFromJsonAsync<GameDetailDto>(Json))!;

        var question = await teacher.PostAsJsonAsync($"/api/admin/games/{game.Id}/questions", new
        {
            prompt = "Match the pairs.",
            order = 1,
            points = 4,
            jsonContent = """{"instructions":"Match.","shuffleRightColumn":true,"pairs":[{"key":"k1","value":"v1"},{"key":"k2","value":"v2"},{"key":"k3","value":"v3"},{"key":"k4","value":"v4"}]}"""
        }, Json);
        question.EnsureSuccessStatusCode();

        var state = await teacher.PostAsJsonAsync($"/api/admin/games/{game.Id}/state", new { state = "Active" }, Json);
        state.EnsureSuccessStatusCode();

        return (await teacher.GetFromJsonAsync<GameDetailDto>($"/api/admin/games/{game.Id}", Json))!;
    }

    // ─── Gameplay ──────────────────────────────────────────────────────────

    public static async Task<StartAttemptResponse> StartAsync(this HttpClient student, int gameId)
    {
        var response = await student.PostAsync($"/api/student/games/{gameId}/start", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StartAttemptResponse>(Json))!;
    }

    public static async Task<AttemptResultDto> SubmitAsync(
        this HttpClient student, int gameId, int questionId, object answer)
    {
        var response = await student.PostAsJsonAsync($"/api/student/games/{gameId}/submit",
            new { answers = new[] { new { questionId, answer } } }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AttemptResultDto>(Json))!;
    }
}
