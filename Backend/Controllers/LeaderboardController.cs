using System.Security.Claims;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

/// <summary>
/// Classroom leaderboards, visible to every authenticated user: the caller's
/// own class (category) plus the global board across all students. Renders
/// exclusively DisplayName + TotalXp — real names and emails never appear
/// here by construction (LeaderboardEntryDto has no PII fields).
/// </summary>
[ApiController]
[Route("api/leaderboard")]
[Authorize]
public class LeaderboardController(
    AppDbContext db,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<LeaderboardsDto>> Get()
    {
        var students = (await userManager.GetUsersInRoleAsync(AppRoles.User))
            .Where(s => s.IsActive)
            .OrderByDescending(s => s.TotalXp)
            .ThenBy(s => s.DisplayName)
            .ToList();

        var global = Rank(students);

        // The caller's class board (teachers have no class → empty).
        var me = await db.Users
            .Include(u => u.Category)
            .FirstAsync(u => u.Id == User.FindFirstValue(ClaimTypes.NameIdentifier));

        var classEntries = me.CategoryId is null
            ? []
            : Rank(students.Where(s => s.CategoryId == me.CategoryId));

        return new LeaderboardsDto(me.Category?.Name, classEntries, global);
    }

    private static List<LeaderboardEntryDto> Rank(IEnumerable<ApplicationUser> students) =>
        students.Select((s, i) => new LeaderboardEntryDto(i + 1, s.DisplayName, s.TotalXp)).ToList();
}
