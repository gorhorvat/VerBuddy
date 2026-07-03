using System.Security.Claims;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

/// <summary>
/// Teacher-only reward catalog management and the application review queue.
/// Rewards are scoped to the admin who created them: an Admin sees/manages
/// only their own rewards and the applications filed against them; SuperAdmin
/// sees everything.
/// </summary>
[ApiController]
[Route("api/admin/rewards")]
[Authorize(Roles = AppRoles.AdminOrSuperAdmin)]
public class AdminRewardsController(AppDbContext db) : ControllerBase
{
    private string CallerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private bool IsSuperAdmin => User.IsInRole(AppRoles.SuperAdmin);

    [HttpGet]
    public async Task<ActionResult<List<RewardDto>>> List()
    {
        var query = db.Rewards.AsQueryable();
        if (!IsSuperAdmin)
            query = query.Where(r => r.CreatedById == CallerId);

        var rewards = await query.OrderBy(r => r.RequiredLevel).ToListAsync();
        return rewards.Select(ToDto).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<RewardDto>> Create(RewardRequest request)
    {
        var reward = new Reward
        {
            Title = request.Title,
            Description = request.Description,
            RequiredLevel = request.RequiredLevel,
            CreatedById = CallerId
        };

        db.Rewards.Add(reward);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(List), ToDto(reward));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<RewardDto>> Update(int id, RewardRequest request)
    {
        var reward = await FindRewardAsync(id);
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
        var reward = await FindRewardAsync(id);
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
        if (!IsSuperAdmin)
            query = query.Where(a => a.Reward.CreatedById == CallerId);
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

    /// <summary>
    /// Pulls back an already-approved reward — e.g. a mistaken approval or a
    /// student who no longer qualifies. Only an Approved application can be
    /// revoked; the resulting Denied status lets the student re-apply.
    /// </summary>
    [HttpPost("applications/{id:int}/revoke")]
    public async Task<ActionResult<RewardApplicationDto>> Revoke(int id)
    {
        var application = await FindApplicationAsync(id);
        if (application is null)
            return NotFound();
        if (application.Status != RewardApplicationStatus.Approved)
            return Conflict(new { message = "Only an approved application can be revoked." });

        application.Status = RewardApplicationStatus.Denied;
        application.DecidedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return ToApplicationDto(application);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Rewards created by the caller — SuperAdmin may reach any.</summary>
    private async Task<Reward?> FindRewardAsync(int id)
    {
        var reward = await db.Rewards.FindAsync(id);
        if (reward is null)
            return null;
        if (!IsSuperAdmin && reward.CreatedById != CallerId)
            return null;
        return reward;
    }

    /// <summary>Applications against rewards the caller created — SuperAdmin may reach any.</summary>
    private async Task<RewardApplication?> FindApplicationAsync(int id)
    {
        var application = await db.RewardApplications
            .Include(a => a.Reward)
            .Include(a => a.Student)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (application is null)
            return null;
        if (!IsSuperAdmin && application.Reward.CreatedById != CallerId)
            return null;
        return application;
    }

    private async Task<ActionResult<RewardApplicationDto>> DecideAsync(int id, RewardApplicationStatus decision)
    {
        var application = await FindApplicationAsync(id);
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
