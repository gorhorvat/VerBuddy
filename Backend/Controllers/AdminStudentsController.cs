using System.Net;
using System.Security.Claims;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

/// <summary>
/// Teacher-only student account management. This is the ONLY controller allowed
/// to return FirstName/LastName/Email (via StudentAdminDto) — the class-level
/// role restriction is the GDPR boundary.
///
/// Account lifecycle: create (no password) → activate (random password emailed,
/// MustChangePassword set) → student changes password on first login.
/// </summary>
[ApiController]
[Route("api/admin/students")]
[Authorize(Roles = AppRoles.AdminOrSuperAdmin)]
public class AdminStudentsController(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    AccountLifecycleService lifecycle) : ControllerBase
{
    /// <summary>All student accounts with their PII, for the admin panel roster.</summary>
    [HttpGet]
    public async Task<ActionResult<List<StudentAdminDto>>> List()
    {
        var students = await userManager.GetUsersInRoleAsync(AppRoles.User);
        var categoryNames = await db.Categories.ToDictionaryAsync(c => c.Id, c => c.Name);

        return students
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => ToDto(s, s.CategoryId is { } cid ? categoryNames.GetValueOrDefault(cid) : null))
            .ToList();
    }

    /// <summary>
    /// Provisions a student account without a password — credentials are
    /// generated and emailed at activation. DisplayName is generated when omitted.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<StudentAdminDto>> Create(CreateStudentRequest request)
    {
        var (student, error) = await CreateStudentAsync(
            request.Username, request.FirstName, request.LastName,
            request.Email, request.DisplayName, request.CategoryId);
        if (error is not null)
            return error.Value.Status == HttpStatusCode.Conflict
                ? Conflict(new { message = error.Value.Message })
                : BadRequest(new { message = error.Value.Message });

        return CreatedAtAction(nameof(List), await ToDtoWithCategoryAsync(student!));
    }

    /// <summary>Bulk creation from a teacher-prepared CSV, parsed client-side.</summary>
    [HttpPost("import")]
    public async Task<ActionResult<ImportStudentsResult>> Import(ImportStudentsRequest request)
    {
        var created = new List<StudentAdminDto>();
        var errors = new List<string>();

        foreach (var row in request.Rows)
        {
            var (student, error) = await CreateStudentAsync(
                row.Username, row.FirstName, row.LastName,
                row.Email, row.DisplayName, request.CategoryId);
            if (error is not null)
                errors.Add($"{row.Username}: {error.Value.Message}");
            else
                created.Add(await ToDtoWithCategoryAsync(student!));
        }

        return new ImportStudentsResult(created, errors);
    }

    /// <summary>Updates PII, nickname (uniqueness enforced) and class assignment.</summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<StudentAdminDto>> Update(string id, UpdateStudentRequest request)
    {
        var student = await FindStudentAsync(id);
        if (student is null)
            return NotFound();

        if (request.CategoryId is { } categoryId)
        {
            // Admins may only file students into their own classes; SuperAdmin
            // into any. "Unknown category." for both so foreign class ids don't
            // leak their existence.
            var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var allowed = User.IsInRole(AppRoles.SuperAdmin)
                ? await db.Categories.AnyAsync(c => c.Id == categoryId)
                : await db.Categories.AnyAsync(c => c.Id == categoryId && c.TeacherId == callerId);
            if (!allowed)
                return BadRequest(new { message = "Unknown category." });
        }

        var displayName = request.DisplayName?.Trim();
        if (!string.IsNullOrEmpty(displayName) && displayName != student.DisplayName)
        {
            if (await userManager.Users.AnyAsync(u => u.Id != student.Id && u.DisplayName == displayName))
                return Conflict(new { message = $"Display name '{displayName}' is already taken." });
            student.DisplayName = displayName;
        }

        student.FirstName = request.FirstName;
        student.LastName = request.LastName;
        student.Email = request.Email;
        student.CategoryId = request.CategoryId;

        var result = await userManager.UpdateAsync(student);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        return await ToDtoWithCategoryAsync(student);
    }

    /// <summary>
    /// Sends (or re-sends) the activation email: generates a fresh random
    /// password, emails it, and forces a password change on first login.
    /// </summary>
    [HttpPost("{id}/activate")]
    public async Task<ActionResult<StudentAdminDto>> Activate(string id)
    {
        var student = await FindStudentAsync(id);
        if (student is null)
            return NotFound();

        var error = await lifecycle.ActivateAsync(student);
        if (error is not null)
            return BadRequest(new { message = error });

        return await ToDtoWithCategoryAsync(student);
    }

    /// <summary>Activates several students at once (e.g. after a CSV import).</summary>
    [HttpPost("activate-bulk")]
    public async Task<ActionResult<BulkActivateResult>> ActivateBulk(BulkActivateRequest request)
    {
        var activated = 0;
        var errors = new List<string>();

        foreach (var id in request.StudentIds.Distinct())
        {
            var student = await FindStudentAsync(id);
            if (student is null)
            {
                errors.Add($"{id}: not found");
                continue;
            }
            var error = await lifecycle.ActivateAsync(student);
            if (error is not null)
                errors.Add($"{student.UserName}: {error}");
            else
                activated++;
        }

        return new BulkActivateResult(activated, errors);
    }

    /// <summary>Emails the student a single-use link to reset their password.</summary>
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> SendPasswordReset(string id)
    {
        var student = await FindStudentAsync(id);
        if (student is null)
            return NotFound();

        var error = await lifecycle.SendPasswordResetAsync(student, "Your teacher");
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

    /// <summary>Blocks login while keeping the student's history and XP.</summary>
    [HttpPost("{id}/deactivate")]
    public async Task<ActionResult<StudentAdminDto>> Deactivate(string id)
    {
        var student = await FindStudentAsync(id);
        if (student is null)
            return NotFound();

        student.IsActive = false;
        await userManager.UpdateAsync(student);
        return await ToDtoWithCategoryAsync(student);
    }

    [HttpPost("{id}/reactivate")]
    public async Task<ActionResult<StudentAdminDto>> Reactivate(string id)
    {
        var student = await FindStudentAsync(id);
        if (student is null)
            return NotFound();

        student.IsActive = true;
        await userManager.UpdateAsync(student);
        return await ToDtoWithCategoryAsync(student);
    }

    /// <summary>
    /// Hard delete — only for accounts with no recorded attempts (GDPR erasure
    /// of a mistyped/never-used account). Students with history should be
    /// deactivated instead so scores and the leaderboard stay consistent.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var student = await FindStudentAsync(id);
        if (student is null)
            return NotFound();

        if (await db.StudentAttempts.AnyAsync(a => a.StudentId == id))
            return Conflict(new { message = "This student has recorded attempts. Deactivate the account instead." });

        await userManager.DeleteAsync(student);
        return NoContent();
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    private async Task<ApplicationUser?> FindStudentAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null || !await userManager.IsInRoleAsync(user, AppRoles.User))
            return null;
        return user;
    }

    private async Task<(ApplicationUser? Student, (HttpStatusCode Status, string Message)? Error)> CreateStudentAsync(
        string username, string? firstName, string? lastName,
        string? email, string? displayName, int? categoryId)
    {
        if (categoryId is not null && !await db.Categories.AnyAsync(c => c.Id == categoryId))
            return (null, (HttpStatusCode.BadRequest, "Unknown category."));

        return await lifecycle.CreateAccountAsync(
            AppRoles.User, username, firstName, lastName, email, displayName, categoryId);
    }

    private async Task<StudentAdminDto> ToDtoWithCategoryAsync(ApplicationUser s)
    {
        string? categoryName = null;
        if (s.CategoryId is { } cid)
            categoryName = (await db.Categories.FindAsync(cid))?.Name;
        return ToDto(s, categoryName);
    }

    private static StudentAdminDto ToDto(ApplicationUser u, string? categoryName) =>
        new(u.Id, u.UserName!, u.FirstName, u.LastName, u.Email, u.DisplayName, u.TotalXp,
            u.CategoryId, categoryName, u.IsActive, u.ActivatedAt, u.MustChangePassword);
}
