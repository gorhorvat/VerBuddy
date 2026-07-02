using Backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
        RoleManager<IdentityRole> roleManager)
    {
        await db.Database.MigrateAsync();

        // ── Roles ──────────────────────────────────────────────────────────
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ── Teacher account (PII fields populated — admin scope only) ─────
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
        if (!await userManager.IsInRoleAsync(teacher, AppRoles.Teacher))
            await userManager.AddToRoleAsync(teacher, AppRoles.Teacher);
    }
}
