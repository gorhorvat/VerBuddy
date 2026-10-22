using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Backend.Tests.TestHelpers;

namespace Backend.Tests;

[Collection("Api")]
public class SavepointTests(ApiFactory factory)
{
    private sealed record RevDto(int Rev, DateTime? SavedAt);

    private static object State(string marker) => new
    {
        entries = new[] { new { gameId = marker, minutes = 60 } },
        wishlist = Array.Empty<object>()
    };

    private static async Task<int> CurrentRevAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<RevDto>("/api/savepoint/rev", Json))!.Rev;

    [Fact]
    public async Task SuperAdmin_SavesAndLoadsState_WithRevisions()
    {
        using var admin = await factory.SuperAdminClientAsync();
        var rev = await CurrentRevAsync(admin);

        var save = await admin.PutAsJsonAsync("/api/savepoint/state",
            new { baseRev = rev, version = 4, state = State("elden-ring") }, Json);
        save.EnsureSuccessStatusCode();
        var saved = (await save.Content.ReadFromJsonAsync<RevDto>(Json))!;
        Assert.Equal(rev + 1, saved.Rev);

        using var doc = JsonDocument.Parse(await admin.GetStringAsync("/api/savepoint/state"));
        Assert.Equal(rev + 1, doc.RootElement.GetProperty("rev").GetInt32());
        Assert.Equal(4, doc.RootElement.GetProperty("version").GetInt32());
        Assert.Equal("elden-ring",
            doc.RootElement.GetProperty("state").GetProperty("entries")[0].GetProperty("gameId").GetString());
    }

    [Fact]
    public async Task SaveFromOutdatedCopy_IsRefusedWithConflict()
    {
        using var admin = await factory.SuperAdminClientAsync();
        var rev = await CurrentRevAsync(admin);

        (await admin.PutAsJsonAsync("/api/savepoint/state",
            new { baseRev = rev, version = 4, state = State("first") }, Json)).EnsureSuccessStatusCode();

        // A second device still holding the old rev must not overwrite the newer save.
        var stale = await admin.PutAsJsonAsync("/api/savepoint/state",
            new { baseRev = rev, version = 4, state = State("stale") }, Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(rev + 1, (await stale.Content.ReadFromJsonAsync<RevDto>(Json))!.Rev);

        using var doc = JsonDocument.Parse(await admin.GetStringAsync("/api/savepoint/state"));
        Assert.Equal("first",
            doc.RootElement.GetProperty("state").GetProperty("entries")[0].GetProperty("gameId").GetString());
    }

    [Fact]
    public async Task State_IsSentCompressed_WhenAccepted()
    {
        using var admin = await factory.SuperAdminClientAsync();
        var rev = await CurrentRevAsync(admin);
        (await admin.PutAsJsonAsync("/api/savepoint/state",
            new { baseRev = rev, version = 4, state = State("compressed") }, Json)).EnsureSuccessStatusCode();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/savepoint/state");
        request.Headers.AcceptEncoding.ParseAdd("gzip");
        using var response = await admin.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Contains("gzip", response.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task InvalidState_IsRejected()
    {
        using var admin = await factory.SuperAdminClientAsync();
        var response = await admin.PutAsJsonAsync("/api/savepoint/state",
            new { baseRev = 0, version = 4, state = new { nope = true } }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OnlySuperAdmin_HasAccess()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/savepoint/state")).StatusCode);

        using var teacher = await factory.TeacherClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync("/api/savepoint/state")).StatusCode);
        var write = await teacher.PutAsJsonAsync("/api/savepoint/state",
            new { baseRev = 0, version = 4, state = State("intruder") }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }
}
