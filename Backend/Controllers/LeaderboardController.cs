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
/// Classroom leaderboards, visible to every authenticated user: one board per
/// class (category) the caller belongs to, plus the global board across all
/// students. Renders exclusively DisplayName + TotalXp — real names and
/// emails never appear here by construction (LeaderboardEntryDto has no PII
/// fields).
/// </summary>
[ApiController]
[Route("api/leaderboard")]
[Authorize]
public class LeaderboardController(
    AppDbContext db,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<LeaderboardResponse>> Get()
    {
        var students = (await userManager.GetUsersInRoleAsync(AppRoles.User))
            .Where(s => s.IsActive)
            .OrderByDescending(s => s.TotalXp)
            .ThenBy(s => s.DisplayName)
            .ToList();

        var global = Rank(students);

        // One board per class the caller belongs to (admins/students with no
        // classes get an empty list).
        var me = await db.Users
            .Include(u => u.Categories)
            .FirstAsync(u => u.Id == User.FindFirstValue(ClaimTypes.NameIdentifier));

        var studentCategoryIds = await db.Users
            .Where(u => students.Select(s => s.Id).Contains(u.Id))
            .Select(u => new { u.Id, CategoryIds = u.Categories.Select(c => c.Id).ToList() })
            .ToDictionaryAsync(x => x.Id, x => x.CategoryIds);

        var classes = me.Categories
            .OrderBy(c => c.Name)
            .Select(c => new ClassBoardDto(
                c.Id, c.Name,
                Rank(students.Where(s => studentCategoryIds.GetValueOrDefault(s.Id, []).Contains(c.Id)))))
            .ToList();

        return new LeaderboardResponse(classes, global);
    }

    private static List<LeaderboardEntryDto> Rank(IEnumerable<ApplicationUser> students) =>
        students.Select((s, i) => new LeaderboardEntryDto(i + 1, s.DisplayName, s.TotalXp)).ToList();
}
