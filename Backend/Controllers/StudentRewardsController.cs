using System.Security.Claims;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

/// <summary>
/// Student portal reward catalog: the global reward list with unlock state
/// derived from the caller's own level, and the apply flow. Everything
/// returned here is pseudonymous — no PII, no other students' data.
/// </summary>
[ApiController]
[Route("api/student/rewards")]
[Authorize(Roles = AppRoles.User)]
public class StudentRewardsController(AppDbContext db) : ControllerBase
{
    private string StudentId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<ActionResult<List<StudentRewardDto>>> List()
    {
        var me = StudentId;
        var student = await db.Users.FindAsync(me);
        if (student is null)
            return Unauthorized();

        var level = LevelSystem.LevelForXp(student.TotalXp);

        // Only rewards created by this student's own teacher — a null owner
        // (unassigned/legacy student) sees none; a SuperAdmin-created student
        // sees the SuperAdmin's rewards, exactly as CreatedByAdminId dictates.
        var rewards = await db.Rewards
            .Where(r => r.CreatedById == student.CreatedByAdminId)
            .OrderBy(r => r.RequiredLevel)
            .ToListAsync();
        var myApplications = await db.RewardApplications
            .Where(a => a.StudentId == me)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync();
        var latestByReward = myApplications
            .GroupBy(a => a.RewardId)
            .ToDictionary(g => g.Key, g => g.First()); // newest first, so First() = latest.

        return rewards.Select(r => new StudentRewardDto(
            r.Id, r.Title, r.Description, r.RequiredLevel,
            level >= r.RequiredLevel,
            latestByReward.TryGetValue(r.Id, out var app) ? app.Status.ToString() : null))
            .ToList();
    }

    /// <summary>
    /// Applies for a reward the caller has unlocked. A Denied prior application
    /// may always be re-applied for (creates a fresh row); a Pending or Approved
    /// one blocks a duplicate application.
    /// </summary>
    [HttpPost("{id:int}/apply")]
    public async Task<ActionResult<StudentRewardDto>> Apply(int id)
    {
        var me = StudentId;
        var student = await db.Users.FindAsync(me);
        if (student is null)
            return Unauthorized();

        // Foreign reward (another teacher's, or created by a different owner
        // than this student's own admin) is treated as not found.
        var reward = await db.Rewards.FindAsync(id);
        if (reward is null || reward.CreatedById != student.CreatedByAdminId)
            return NotFound();

        var level = LevelSystem.LevelForXp(student.TotalXp);
        if (level < reward.RequiredLevel)
            return BadRequest(new { message = $"You must reach level {reward.RequiredLevel} to apply for this reward." });

        var existing = await db.RewardApplications
            .Where(a => a.StudentId == me && a.RewardId == id)
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync();
        if (existing is { Status: RewardApplicationStatus.Pending or RewardApplicationStatus.Approved })
            return Conflict(new { message = "You already have an application for this reward." });

        db.RewardApplications.Add(new RewardApplication
        {
            RewardId = id,
            StudentId = me
        });
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(List), new StudentRewardDto(
            reward.Id, reward.Title, reward.Description, reward.RequiredLevel,
            true, RewardApplicationStatus.Pending.ToString()));
    }
}
