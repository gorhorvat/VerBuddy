using System.Net;
using System.Net.Http.Json;
using Backend.DTOs;
using Backend.Models;
using static Backend.Tests.TestHelpers;

namespace Backend.Tests;

[Collection("Api")]
public class AdminManagementTests(ApiFactory factory)
{
    [Fact]
    public async Task Seeded_superadmin_logs_in_with_superadmin_role()
    {
        var auth = await factory.LoginAsync(SuperAdminUsername, SuperAdminPassword);
        Assert.Contains(AppRoles.SuperAdmin, auth.Roles);
        Assert.DoesNotContain(AppRoles.Admin, auth.Roles);
    }

    [Fact]
    public async Task Admin_and_anonymous_cannot_access_superadmin_endpoints()
    {
        using var admin = await factory.TeacherClientAsync();
        var forbidden = await admin.GetAsync("/api/superadmin/admins");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var anonymous = factory.CreateClient();
        var unauthorized = await anonymous.GetAsync("/api/superadmin/admins");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_creates_and_activates_admin_who_can_manage_students()
    {
        using var super = await factory.SuperAdminClientAsync();
        var created = await factory.CreateActivatedAdminAsync(super);

        // The new admin can call an Admin-scoped endpoint.
        var auth = await factory.LoginAsync(created.Username, created.Password);
        Assert.Contains(AppRoles.Admin, auth.Roles);
        using var adminClient = factory.ClientWithToken(auth.Token);
        var students = await adminClient.GetAsync("/api/admin/students");
        Assert.Equal(HttpStatusCode.OK, students.StatusCode);

        // And appears in the roster.
        var roster = await super.GetFromJsonAsync<List<AdminAccountDto>>("/api/superadmin/admins", Json);
        Assert.Contains(roster!, a => a.Id == created.Id);
    }

    [Fact]
    public async Task SuperAdmin_can_access_admin_scoped_endpoints()
    {
        using var super = await factory.SuperAdminClientAsync();
        var students = await super.GetAsync("/api/admin/students");
        Assert.Equal(HttpStatusCode.OK, students.StatusCode);
    }

    [Fact]
    public async Task Student_id_returns_404_on_admins_endpoints()
    {
        using var super = await factory.SuperAdminClientAsync();
        using var teacher = await factory.TeacherClientAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);

        var response = await super.PostAsync($"/api/superadmin/admins/{student.Id}/activate", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_display_name_returns_conflict()
    {
        using var super = await factory.SuperAdminClientAsync();
        var username = Unique("adm");
        var first = await super.PostAsJsonAsync("/api/superadmin/admins", new
        {
            username,
            firstName = "Dup",
            lastName = "Name",
            email = $"{username}@test.local",
            displayName = $"Dup-{username}"
        }, Json);
        first.EnsureSuccessStatusCode();

        var second = await super.PostAsJsonAsync("/api/superadmin/admins", new
        {
            username = Unique("adm"),
            firstName = "Dup",
            lastName = "Name",
            email = "other@test.local",
            displayName = $"Dup-{username}"
        }, Json);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
}
