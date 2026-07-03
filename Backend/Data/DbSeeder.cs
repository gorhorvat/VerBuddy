using Backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Backend.Data;

/// <summary>
/// Development seeder: ensures the roles and the teacher account exist.
/// Idempotent — safe to run on every startup.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        ILogger? logger = null)
    {
        await db.Database.MigrateAsync();

        // ── Legacy role rename (Teacher→Admin, Student→User) ──────────────
        // In-place rename keeps existing user-role assignments, which
        // reference the role Id, not its name.
        foreach (var (oldName, newName) in new[] { ("Teacher", AppRoles.Admin), ("Student", AppRoles.User) })
        {
            var legacy = await roleManager.FindByNameAsync(oldName);
            if (legacy is not null && await roleManager.FindByNameAsync(newName) is null)
            {
                await roleManager.SetRoleNameAsync(legacy, newName);
                await roleManager.UpdateAsync(legacy); // Persists Name + NormalizedName.
            }
        }

        // ── Roles ──────────────────────────────────────────────────────────
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ── Demo teacher account (dev/tests only — Seed:DemoAccounts) ─────
        // Never seeded in production: known password.
        if (configuration.GetValue("Seed:DemoAccounts", false))
        {
            var teacher = await userManager.FindByNameAsync("teacher.anna");
            if (teacher is null)
            {
                teacher = new ApplicationUser
                {
                    UserName = "teacher.anna",
                    Email = "anna@example-school.test",
                    FirstName = "Anna",
                    LastName = "Kovacs",
                    DisplayName = "Ms. Anna",
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(teacher, "ChangeMe!123");
            }
            if (!await userManager.IsInRoleAsync(teacher, AppRoles.Admin))
                await userManager.AddToRoleAsync(teacher, AppRoles.Admin);
        }

        // ── SuperAdmin (from configuration — never hardcoded) ─────────────
        var superUsername = configuration["SuperAdmin:UserName"];
        var superPassword = configuration["SuperAdmin:Password"];
        if (string.IsNullOrWhiteSpace(superUsername) || string.IsNullOrWhiteSpace(superPassword))
        {
            logger?.LogWarning(
                "SuperAdmin:UserName / SuperAdmin:Password are not configured — no SuperAdmin account was seeded.");
            return;
        }

        var super = await userManager.FindByNameAsync(superUsername);
        if (super is null)
        {
            super = new ApplicationUser
            {
                UserName = superUsername,
                Email = configuration["SuperAdmin:Email"],
                DisplayName = "Super Admin",
                EmailConfirmed = true
            };
            var created = await userManager.CreateAsync(super, superPassword);
            if (!created.Succeeded)
                throw new InvalidOperationException(
                    "SuperAdmin seeding failed: " + string.Join(' ', created.Errors.Select(e => e.Description)));
        }
        if (!await userManager.IsInRoleAsync(super, AppRoles.SuperAdmin))
            await userManager.AddToRoleAsync(super, AppRoles.SuperAdmin);
    }
}
