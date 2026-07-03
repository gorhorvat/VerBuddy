using System.Net;
using System.Net.Http.Json;
using Backend.DTOs;
using static Backend.Tests.TestHelpers;

namespace Backend.Tests;

/// <summary>
/// Ownership scoping: a teacher (Admin) sees/manages only the students and
/// rewards they created; SuperAdmin sees everything; students see only their
/// own teacher's rewards. Also covers reward-application revocation.
/// </summary>
[Collection("Api")]
public class OwnershipScopingTests(ApiFactory factory)
{
    private static async Task<RewardDto> CreateRewardAsync(HttpClient admin, int requiredLevel = 0, string? title = null)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/rewards", new
        {
            title = title ?? Unique("Reward"),
            description = "A shiny prize.",
            requiredLevel
        }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RewardDto>(Json))!;
    }

    // ─── Student ownership ─────────────────────────────────────────────────

    [Fact]
    public async Task TeacherB_cannot_see_or_edit_teacherA_student()
    {
        using var teacherA = await factory.TeacherClientAsync();
        var student = await factory.CreateActivatedStudentAsync(teacherA);

        using var super = await factory.SuperAdminClientAsync();
        var teacherBAccount = await factory.CreateActivatedAdminAsync(super);
        using var teacherB = await factory.AdminClientAsync(teacherBAccount);

        // List excludes teacher A's student.
        var list = await teacherB.GetFromJsonAsync<List<StudentAdminDto>>("/api/admin/students", Json);
        Assert.DoesNotContain(list!, s => s.Id == student.Id);

        // Every id-scoped path 404s for teacher B.
        var update = await teacherB.PutAsJsonAsync($"/api/admin/students/{student.Id}", new
        {
            firstName = "X",
            lastName = "Y",
            email = (string?)null,
            displayName = (string?)null,
            categoryIds = (int[]?)null
        }, Json);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);

        var activate = await teacherB.PostAsync($"/api/admin/students/{student.Id}/activate", null);
        Assert.Equal(HttpStatusCode.NotFound, activate.StatusCode);

        var deactivate = await teacherB.PostAsync($"/api/admin/students/{student.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.NotFound, deactivate.StatusCode);

        var resetPassword = await teacherB.PostAsync($"/api/admin/students/{student.Id}/reset-password", null);
        Assert.Equal(HttpStatusCode.NotFound, resetPassword.StatusCode);

        var delete = await teacherB.DeleteAsync($"/api/admin/students/{student.Id}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

        // Teacher A still sees their own student.
        var ownList = await teacherA.GetFromJsonAsync<List<StudentAdminDto>>("/api/admin/students", Json);
        Assert.Contains(ownList!, s => s.Id == student.Id);
    }

    [Fact]
    public async Task SuperAdminCreated_student_invisible_to_teacher_but_visible_to_superadmin()
    {
        using var super = await factory.SuperAdminClientAsync();
        var student = await factory.CreateActivatedStudentAsync(super);

        using var teacher = await factory.TeacherClientAsync();
        var teacherList = await teacher.GetFromJsonAsync<List<StudentAdminDto>>("/api/admin/students", Json);
        Assert.DoesNotContain(teacherList!, s => s.Id == student.Id);

        var teacherGet = await teacher.PutAsJsonAsync($"/api/admin/students/{student.Id}", new
        {
            firstName = "X",
            lastName = "Y",
            email = (string?)null,
            displayName = (string?)null,
            categoryIds = (int[]?)null
        }, Json);
        Assert.Equal(HttpStatusCode.NotFound, teacherGet.StatusCode);

        var superList = await super.GetFromJsonAsync<List<StudentAdminDto>>("/api/admin/students", Json);
        Assert.Contains(superList!, s => s.Id == student.Id);
    }

    // ─── Reward ownership ──────────────────────────────────────────────────

    [Fact]
    public async Task TeacherB_sees_no_rewards_or_applications_of_teacherA_but_superadmin_sees_all()
    {
        using var teacherA = await factory.TeacherClientAsync();
        var reward = await CreateRewardAsync(teacherA);
        var student = await factory.CreateActivatedStudentAsync(teacherA);
        using var studentClient = await factory.StudentClientAsync(student);
        var apply = await studentClient.PostAsync($"/api/student/rewards/{reward.Id}/apply", null);
        apply.EnsureSuccessStatusCode();

        using var super = await factory.SuperAdminClientAsync();
        var teacherBAccount = await factory.CreateActivatedAdminAsync(super);
        using var teacherB = await factory.AdminClientAsync(teacherBAccount);

        // Reward list excludes teacher A's reward.
        var rewardList = await teacherB.GetFromJsonAsync<List<RewardDto>>("/api/admin/rewards", Json);
        Assert.DoesNotContain(rewardList!, r => r.Id == reward.Id);

        // Update/Delete 404 for teacher B.
        var update = await teacherB.PutAsJsonAsync($"/api/admin/rewards/{reward.Id}", new
        {
            title = "Hijacked",
            description = (string?)null,
            requiredLevel = 0
        }, Json);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);

        var delete = await teacherB.DeleteAsync($"/api/admin/rewards/{reward.Id}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

        // Applications queue excludes teacher A's applications.
        var applications = await teacherB.GetFromJsonAsync<List<RewardApplicationDto>>("/api/admin/rewards/applications", Json);
        Assert.DoesNotContain(applications!, a => a.RewardId == reward.Id);

        // Approve/Deny/Revoke on a foreign application 404 for teacher B.
        var pendingA = await teacherA.GetFromJsonAsync<List<RewardApplicationDto>>(
            "/api/admin/rewards/applications?status=Pending", Json);
        var applicationId = pendingA!.Single(a => a.RewardId == reward.Id).Id;

        var approveForeign = await teacherB.PostAsync($"/api/admin/rewards/applications/{applicationId}/approve", null);
        Assert.Equal(HttpStatusCode.NotFound, approveForeign.StatusCode);

        // SuperAdmin sees everything.
        var superRewards = await super.GetFromJsonAsync<List<RewardDto>>("/api/admin/rewards", Json);
        Assert.Contains(superRewards!, r => r.Id == reward.Id);

        var superApplications = await super.GetFromJsonAsync<List<RewardApplicationDto>>("/api/admin/rewards/applications", Json);
        Assert.Contains(superApplications!, a => a.RewardId == reward.Id);
    }

    [Fact]
    public async Task Student_sees_only_own_teachers_rewards()
    {
        using var teacherA = await factory.TeacherClientAsync();
        var studentA = await factory.CreateActivatedStudentAsync(teacherA);
        var rewardA = await CreateRewardAsync(teacherA, title: Unique("RewardA"));

        using var super = await factory.SuperAdminClientAsync();
        var teacherBAccount = await factory.CreateActivatedAdminAsync(super);
        using var teacherB = await factory.AdminClientAsync(teacherBAccount);
        var rewardB = await CreateRewardAsync(teacherB, title: Unique("RewardB"));

        using var studentAClient = await factory.StudentClientAsync(studentA);
        var visible = await studentAClient.GetFromJsonAsync<List<StudentRewardDto>>("/api/student/rewards", Json);
        Assert.Contains(visible!, r => r.Id == rewardA.Id);
        Assert.DoesNotContain(visible!, r => r.Id == rewardB.Id);

        // Applying to the foreign teacher's reward 404s.
        var applyForeign = await studentAClient.PostAsync($"/api/student/rewards/{rewardB.Id}/apply", null);
        Assert.Equal(HttpStatusCode.NotFound, applyForeign.StatusCode);
    }

    // ─── Revoke ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Revoke_approved_application_denies_and_allows_reapply()
    {
        using var teacher = await factory.TeacherClientAsync();
        var reward = await CreateRewardAsync(teacher, requiredLevel: 0);
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var studentClient = await factory.StudentClientAsync(student);

        var apply = await studentClient.PostAsync($"/api/student/rewards/{reward.Id}/apply", null);
        apply.EnsureSuccessStatusCode();

        var pending = await teacher.GetFromJsonAsync<List<RewardApplicationDto>>(
            "/api/admin/rewards/applications?status=Pending", Json);
        var applicationId = pending!.Single(a => a.RewardId == reward.Id).Id;

        var approve = await teacher.PostAsync($"/api/admin/rewards/applications/{applicationId}/approve", null);
        approve.EnsureSuccessStatusCode();

        var revoke = await teacher.PostAsync($"/api/admin/rewards/applications/{applicationId}/revoke", null);
        revoke.EnsureSuccessStatusCode();
        var revoked = (await revoke.Content.ReadFromJsonAsync<RewardApplicationDto>(Json))!;
        Assert.Equal("Denied", revoked.Status);
        Assert.NotNull(revoked.DecidedAtUtc);

        // Student now sees Denied and may re-apply.
        var listAfter = await studentClient.GetFromJsonAsync<List<StudentRewardDto>>("/api/student/rewards", Json);
        Assert.Equal("Denied", listAfter!.Single(r => r.Id == reward.Id).MyApplicationStatus);

        var reapply = await studentClient.PostAsync($"/api/student/rewards/{reward.Id}/apply", null);
        Assert.Equal(HttpStatusCode.Created, reapply.StatusCode);
    }

    [Fact]
    public async Task Revoke_non_approved_application_returns_conflict()
    {
        using var teacher = await factory.TeacherClientAsync();
        var reward = await CreateRewardAsync(teacher, requiredLevel: 0);
        var student = await factory.CreateActivatedStudentAsync(teacher);
        using var studentClient = await factory.StudentClientAsync(student);

        var apply = await studentClient.PostAsync($"/api/student/rewards/{reward.Id}/apply", null);
        apply.EnsureSuccessStatusCode();

        var pending = await teacher.GetFromJsonAsync<List<RewardApplicationDto>>(
            "/api/admin/rewards/applications?status=Pending", Json);
        var applicationId = pending!.Single(a => a.RewardId == reward.Id).Id;

        // Still Pending — revoke requires Approved.
        var revoke = await teacher.PostAsync($"/api/admin/rewards/applications/{applicationId}/revoke", null);
        Assert.Equal(HttpStatusCode.Conflict, revoke.StatusCode);
    }

    [Fact]
    public async Task Revoke_unknown_application_returns_404()
    {
        using var teacher = await factory.TeacherClientAsync();
        var response = await teacher.PostAsync("/api/admin/rewards/applications/999999/revoke", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
