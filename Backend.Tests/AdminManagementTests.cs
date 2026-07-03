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
}
