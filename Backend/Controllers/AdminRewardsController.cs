using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

/// <summary>
/// Teacher-only reward catalog management and the application review queue.
/// Rewards are global (not filed by class) — every student sees the same list.
/// </summary>
[ApiController]
[Route("api/admin/rewards")]
[Authorize(Roles = AppRoles.AdminOrSuperAdmin)]
public class AdminRewardsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RewardDto>>> List()
    {
        var rewards = await db.Rewards.OrderBy(r => r.RequiredLevel).ToListAsync();
        return rewards.Select(ToDto).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<RewardDto>> Create(RewardRequest request)
    {
        var reward = new Reward
        {
            Title = request.Title,
            Description = request.Description,
            RequiredLevel = request.RequiredLevel
        };

        db.Rewards.Add(reward);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(List), ToDto(reward));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<RewardDto>> Update(int id, RewardRequest request)
    {
        var reward = await db.Rewards.FindAsync(id);
        if (reward is null)
            return NotFound();

        reward.Title = request.Title;
        reward.Description = request.Description;
        reward.RequiredLevel = request.RequiredLevel;
        await db.SaveChangesAsync();

        return ToDto(reward);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var reward = await db.Rewards.FindAsync(id);
        if (reward is null)
            return NotFound();

        db.Rewards.Remove(reward); // Applications cascade-delete.
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>The review queue, optionally filtered by status, newest first.</summary>
    [HttpGet("applications")]
    public async Task<ActionResult<List<RewardApplicationDto>>> Applications(RewardApplicationStatus? status = null)
    {
        var query = db.RewardApplications
            .Include(a => a.Reward)
            .Include(a => a.Student)
            .AsQueryable();
        if (status is not null)
            query = query.Where(a => a.Status == status);

        var applications = await query.OrderByDescending(a => a.CreatedAtUtc).ToListAsync();
        return applications.Select(ToApplicationDto).ToList();
    }

    [HttpPost("applications/{id:int}/approve")]
    public Task<ActionResult<RewardApplicationDto>> Approve(int id) =>
        DecideAsync(id, RewardApplicationStatus.Approved);

    [HttpPost("applications/{id:int}/deny")]
    public Task<ActionResult<RewardApplicationDto>> Deny(int id) =>
        DecideAsync(id, RewardApplicationStatus.Denied);

    // ─── Helpers ───────────────────────────────────────────────────────────

    private async Task<ActionResult<RewardApplicationDto>> DecideAsync(int id, RewardApplicationStatus decision)
    {
        var application = await db.RewardApplications
            .Include(a => a.Reward)
            .Include(a => a.Student)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (application is null)
            return NotFound();
        if (application.Status != RewardApplicationStatus.Pending)
            return Conflict(new { message = "This application has already been decided." });

        application.Status = decision;
        application.DecidedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return ToApplicationDto(application);
    }

    private static RewardDto ToDto(Reward r) =>
        new(r.Id, r.Title, r.Description, r.RequiredLevel);

    private static RewardApplicationDto ToApplicationDto(RewardApplication a) =>
        new(a.Id, a.RewardId, a.Reward.Title, a.Reward.RequiredLevel, a.Student.DisplayName,
            a.Status.ToString(), a.CreatedAtUtc, a.DecidedAtUtc);
}
