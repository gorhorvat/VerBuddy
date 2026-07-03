using System.Net;
using System.Net.Http.Json;
using Backend.DTOs;
using static Backend.Tests.TestHelpers;

namespace Backend.Tests;

[Collection("Api")]
public class RewardsTests(ApiFactory factory)
{
    private static async Task<RewardDto> CreateRewardAsync(HttpClient teacher, int requiredLevel, string? title = null)
    {
        var response = await teacher.PostAsJsonAsync("/api/admin/rewards", new
        {
            title = title ?? Unique("Reward"),
            description = "A shiny prize.",
            requiredLevel
        }, Json);
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RewardDto>(Json))!;
    }

    [Fact]
    public async Task Admin_CanCreateUpdateAndDeleteReward()
    {
        using var teacher = await factory.TeacherClientAsync();

        var reward = await CreateRewardAsync(teacher, requiredLevel: 5, title: Unique("CrudReward"));
        Assert.Equal(5, reward.RequiredLevel);

        var update = await teacher.PutAsJsonAsync($"/api/admin/rewards/{reward.Id}", new
        {
            title = "Updated Title",
            description = "Updated.",
            requiredLevel = 10
        }, Json);
        update.EnsureSuccessStatusCode();
        var updated = (await update.Content.ReadFromJsonAsync<RewardDto>(Json))!;
        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal(10, updated.RequiredLevel);

        var delete = await teacher.DeleteAsync($"/api/admin/rewards/{reward.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var missingUpdate = await teacher.PutAsJsonAsync($"/api/admin/rewards/{reward.Id}", new
        {
            title = "Gone",
            description = (string?)null,
            requiredLevel = 0
        }, Json);
        Assert.Equal(HttpStatusCode.NotFound, missingUpdate.StatusCode);
    }

    [Fact]
    public async Task List_IsOrderedByRequiredLevel()
    {
        using var teacher = await factory.TeacherClientAsync();
        await CreateRewardAsync(teacher, requiredLevel: 20);
        await CreateRewardAsync(teacher, requiredLevel: 1);

        var rewards = await teacher.GetFromJsonAsync<List<RewardDto>>("/api/admin/rewards", Json);

        Assert.NotNull(rewards);
        for (var i = 1; i < rewards!.Count; i++)
            Assert.True(rewards[i - 1].RequiredLevel <= rewards[i].RequiredLevel);
    }

    [Fact]
    public async Task Student_BelowRequiredLevel_SeesRewardLockedAndCannotApply()
    {
        using var teacher = await factory.TeacherClientAsync();
        var reward = await CreateRewardAsync(teacher, requiredLevel: 5);
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);

        var rewards = await client.GetFromJsonAsync<List<StudentRewardDto>>("/api/student/rewards", Json);
        var mine = rewards!.Single(r => r.Id == reward.Id);
        Assert.False(mine.Unlocked);
        Assert.Null(mine.MyApplicationStatus);

        var apply = await client.PostAsync($"/api/student/rewards/{reward.Id}/apply", null);
        Assert.Equal(HttpStatusCode.BadRequest, apply.StatusCode);
    }

    [Fact]
    public async Task Student_UnlockedReward_FullApplyApproveFlow()
    {
        using var teacher = await factory.TeacherClientAsync();
        // RequiredLevel 0: every student (level 0 at creation) is unlocked immediately.
        var reward = await CreateRewardAsync(teacher, requiredLevel: 0);
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);

        var listBefore = await client.GetFromJsonAsync<List<StudentRewardDto>>("/api/student/rewards", Json);
        var beforeApply = listBefore!.Single(r => r.Id == reward.Id);
        Assert.True(beforeApply.Unlocked);
        Assert.Null(beforeApply.MyApplicationStatus);

        var apply = await client.PostAsync($"/api/student/rewards/{reward.Id}/apply", null);
        Assert.Equal(HttpStatusCode.Created, apply.StatusCode);
        var applied = (await apply.Content.ReadFromJsonAsync<StudentRewardDto>(Json))!;
        Assert.Equal("Pending", applied.MyApplicationStatus);

        // Duplicate apply while Pending -> 409.
        var secondApply = await client.PostAsync($"/api/student/rewards/{reward.Id}/apply", null);
        Assert.Equal(HttpStatusCode.Conflict, secondApply.StatusCode);

        // Admin sees it in the Pending queue.
        var pendingQueue = await teacher.GetFromJsonAsync<List<RewardApplicationDto>>(
            "/api/admin/rewards/applications?status=Pending", Json);
        var applicationDto = pendingQueue!.Single(a => a.RewardId == reward.Id);
        Assert.Equal(student.DisplayName, applicationDto.StudentDisplayName);
        Assert.Equal("Pending", applicationDto.Status);

        // Approve.
        var approve = await teacher.PostAsync($"/api/admin/rewards/applications/{applicationDto.Id}/approve", null);
        approve.EnsureSuccessStatusCode();
        var approved = (await approve.Content.ReadFromJsonAsync<RewardApplicationDto>(Json))!;
        Assert.Equal("Approved", approved.Status);
        Assert.NotNull(approved.DecidedAtUtc);

        // Approving again -> 409 (already decided).
        var reapprove = await teacher.PostAsync($"/api/admin/rewards/applications/{applicationDto.Id}/approve", null);
        Assert.Equal(HttpStatusCode.Conflict, reapprove.StatusCode);

        // Student now sees Approved.
        var listAfter = await client.GetFromJsonAsync<List<StudentRewardDto>>("/api/student/rewards", Json);
        Assert.Equal("Approved", listAfter!.Single(r => r.Id == reward.Id).MyApplicationStatus);

        // While Approved, re-applying is blocked.
        var applyAgain = await client.PostAsync($"/api/student/rewards/{reward.Id}/apply", null);
        Assert.Equal(HttpStatusCode.Conflict, applyAgain.StatusCode);
    }

    [Fact]
    public async Task Student_DeniedApplication_MayReapply()
    {
        using var teacher = await factory.TeacherClientAsync();
        var reward = await CreateRewardAsync(teacher, requiredLevel: 0);
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);

        var apply = await client.PostAsync($"/api/student/rewards/{reward.Id}/apply", null);
        apply.EnsureSuccessStatusCode();

        var pending = await teacher.GetFromJsonAsync<List<RewardApplicationDto>>(
            "/api/admin/rewards/applications?status=Pending", Json);
        var applicationDto = pending!.Single(a => a.RewardId == reward.Id);

        var deny = await teacher.PostAsync($"/api/admin/rewards/applications/{applicationDto.Id}/deny", null);
        deny.EnsureSuccessStatusCode();
        var denied = (await deny.Content.ReadFromJsonAsync<RewardApplicationDto>(Json))!;
        Assert.Equal("Denied", denied.Status);

        // Student sees Denied, but a fresh apply is allowed.
        var listAfterDeny = await client.GetFromJsonAsync<List<StudentRewardDto>>("/api/student/rewards", Json);
        Assert.Equal("Denied", listAfterDeny!.Single(r => r.Id == reward.Id).MyApplicationStatus);

        var reapply = await client.PostAsync($"/api/student/rewards/{reward.Id}/apply", null);
        Assert.Equal(HttpStatusCode.Created, reapply.StatusCode);
        var reapplied = (await reapply.Content.ReadFromJsonAsync<StudentRewardDto>(Json))!;
        Assert.Equal("Pending", reapplied.MyApplicationStatus);

        // Now there should be a fresh Pending application distinct from the denied one.
        var deniedQueue = await teacher.GetFromJsonAsync<List<RewardApplicationDto>>(
            "/api/admin/rewards/applications?status=Denied", Json);
        Assert.Contains(deniedQueue!, a => a.RewardId == reward.Id);

        var pendingQueue2 = await teacher.GetFromJsonAsync<List<RewardApplicationDto>>(
            "/api/admin/rewards/applications?status=Pending", Json);
        Assert.Contains(pendingQueue2!, a => a.RewardId == reward.Id);
    }

    [Fact]
    public async Task ApplyToUnknownReward_Returns404()
    {
        using var teacher = await factory.TeacherClientAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);

        var response = await client.PostAsync("/api/student/rewards/999999/apply", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StudentToken_CannotAccessAdminRewardEndpoints()
    {
        using var teacher = await factory.TeacherClientAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var client = await factory.StudentClientAsync(student);

        var response = await client.GetAsync("/api/admin/rewards");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
