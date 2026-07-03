using System.Net;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

/// <summary>
/// SuperAdmin-only management of Admin accounts — the only place admin
/// accounts are created. Mirrors the student lifecycle: create (no password)
/// → activate (random password emailed, MustChangePassword set) → admin
/// changes password on first login. The panel never creates SuperAdmins.
/// </summary>
[ApiController]
[Route("api/superadmin/admins")]
[Authorize(Roles = AppRoles.SuperAdmin)]
public class AdminsController(
    Data.AppDbContext db,
    UserManager<ApplicationUser> userManager,
    AccountLifecycleService lifecycle) : ControllerBase
{
    /// <summary>All Admin-role accounts with their PII, for the superadmin panel.</summary>
    [HttpGet]
    public async Task<ActionResult<List<AdminAccountDto>>> List()
    {
        var admins = await userManager.GetUsersInRoleAsync(AppRoles.Admin);
        return admins
            .OrderBy(a => a.LastName).ThenBy(a => a.FirstName)
            .Select(ToDto)
            .ToList();
    }

    /// <summary>
    /// Provisions an admin account without a password — credentials are
    /// generated and emailed at activation.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AdminAccountDto>> Create(CreateAdminRequest request)
    {
        var (admin, error) = await lifecycle.CreateAccountAsync(
            AppRoles.Admin, request.Username, request.FirstName, request.LastName,
            request.Email, request.DisplayName);
        if (error is not null)
            return error.Value.Status == HttpStatusCode.Conflict
                ? Conflict(new { message = error.Value.Message })
                : BadRequest(new { message = error.Value.Message });

        return CreatedAtAction(nameof(List), ToDto(admin!));
    }

    /// <summary>
    /// Sends (or re-sends) the activation email: generates a fresh random
    /// password, emails it, and forces a password change on first login.
    /// </summary>
    [HttpPost("{id}/activate")]
    public async Task<ActionResult<AdminAccountDto>> Activate(string id)
    {
        var admin = await FindAdminAsync(id);
        if (admin is null)
            return NotFound();

        var error = await lifecycle.ActivateAsync(admin);
        if (error is not null)
            return BadRequest(new { message = error });

        return ToDto(admin);
    }

    /// <summary>Updates PII + display name (uniqueness enforced).</summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<AdminAccountDto>> Update(string id, UpdateAdminRequest request)
    {
        var admin = await FindAdminAsync(id);
        if (admin is null)
            return NotFound();

        var displayName = request.DisplayName?.Trim();
        if (!string.IsNullOrEmpty(displayName) && displayName != admin.DisplayName)
        {
            if (await userManager.Users.AnyAsync(u => u.Id != admin.Id && u.DisplayName == displayName))
                return Conflict(new { message = $"Display name '{displayName}' is already taken." });
            admin.DisplayName = displayName;
        }

        admin.FirstName = request.FirstName;
        admin.LastName = request.LastName;
        admin.Email = request.Email;

        var result = await userManager.UpdateAsync(admin);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        return ToDto(admin);
    }

    /// <summary>Emails the admin a single-use link to reset their password.</summary>
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> SendPasswordReset(string id)
    {
        var admin = await FindAdminAsync(id);
        if (admin is null)
            return NotFound();

        var error = await lifecycle.SendPasswordResetAsync(admin, "A super admin");
        if (error is not null)
            return error.Value.Status switch
            {
                HttpStatusCode.Conflict => Conflict(new { message = error.Value.Message }),
                HttpStatusCode.BadGateway => StatusCode(StatusCodes.Status502BadGateway,
                    new { message = error.Value.Message }),
                _ => BadRequest(new { message = error.Value.Message })
            };

        return NoContent();
    }

    /// <summary>Blocks login while keeping the account.</summary>
    [HttpPost("{id}/deactivate")]
    public async Task<ActionResult<AdminAccountDto>> Deactivate(string id)
    {
        var admin = await FindAdminAsync(id);
        if (admin is null)
            return NotFound();

        admin.IsActive = false;
        await userManager.UpdateAsync(admin);
        return ToDto(admin);
    }

    [HttpPost("{id}/reactivate")]
    public async Task<ActionResult<AdminAccountDto>> Reactivate(string id)
    {
        var admin = await FindAdminAsync(id);
        if (admin is null)
            return NotFound();

        admin.IsActive = true;
        await userManager.UpdateAsync(admin);
        return ToDto(admin);
    }

    /// <summary>
    /// Hard delete. Safe for admins: games/categories/attempts carry no
    /// ownership FK to admin users, so nothing is orphaned.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var admin = await FindAdminAsync(id);
        if (admin is null)
            return NotFound();

        // The admin's rewards go with the account (applications cascade with
        // the reward); the Restrict FK would otherwise block the delete.
        await db.Rewards.Where(r => r.CreatedById == id).ExecuteDeleteAsync();

        await userManager.DeleteAsync(admin);
        return NoContent();
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Admin-role accounts only — SuperAdmin/User ids yield 404.</summary>
    private async Task<ApplicationUser?> FindAdminAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null || !await userManager.IsInRoleAsync(user, AppRoles.Admin))
            return null;
        return user;
    }

    private static AdminAccountDto ToDto(ApplicationUser u) =>
        new(u.Id, u.UserName!, u.FirstName, u.LastName, u.Email, u.DisplayName,
            u.IsActive, u.ActivatedAt, u.MustChangePassword);
}
