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
