using System.Net;
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
    IEmailSender emailSender,
    IConfiguration config) : ControllerBase
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

        var error = await ActivateStudentAsync(student);
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
            var error = await ActivateStudentAsync(student);
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
        if (string.IsNullOrWhiteSpace(student.Email))
            return BadRequest(new { message = "This student has no email address on file." });
        if (!student.IsActive)
            return Conflict(new { message = "This student is deactivated." });

        var token = await userManager.GeneratePasswordResetTokenAsync(student);
        var baseUrl = config["Frontend:BaseUrl"] ?? "http://localhost:5173";
        var link = $"{baseUrl}/reset-password?user={Uri.EscapeDataString(student.Id)}&token={Uri.EscapeDataString(token)}";

        try
        {
            await emailSender.SendAsync(
                student.Email,
                "Reset your VerBuddy password",
                $"""
                 Hi {student.FirstName ?? student.DisplayName},

                 Your teacher requested a password reset for your account ({student.UserName}).
                 Open this link to choose a new password:

                 {link}

                 If you didn't expect this email you can ignore it.
                 """);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = $"The reset email could not be sent — check the SMTP settings. ({ex.Message})" });
        }

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

        displayName = displayName?.Trim();
        if (string.IsNullOrEmpty(displayName))
        {
            do { displayName = DisplayNameGenerator.Generate(); }
            while (await userManager.Users.AnyAsync(u => u.DisplayName == displayName));
        }
        else if (await userManager.Users.AnyAsync(u => u.DisplayName == displayName))
        {
            return (null, (HttpStatusCode.Conflict, $"Display name '{displayName}' is already taken."));
        }

        var student = new ApplicationUser
        {
            UserName = username.Trim(),
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            DisplayName = displayName,
            CategoryId = categoryId,
            EmailConfirmed = true // No email round-trip for classroom accounts.
        };

        // No password: the account is unusable until the teacher activates it.
        var result = await userManager.CreateAsync(student);
        if (!result.Succeeded)
            return (null, (HttpStatusCode.BadRequest, string.Join(' ', result.Errors.Select(e => e.Description))));

        await userManager.AddToRoleAsync(student, AppRoles.User);
        return (student, null);
    }

    /// <summary>Returns an error message, or null on success.</summary>
    private async Task<string?> ActivateStudentAsync(ApplicationUser student)
    {
        if (string.IsNullOrWhiteSpace(student.Email))
            return "No email address on file — add one before activating.";
        if (!student.IsActive)
            return "The account is deactivated.";

        // A fresh password is generated at activation (and on re-activation
        // emails), so plaintext credentials never need to be stored. The email
        // goes out FIRST: if delivery fails, the account is left untouched and
        // the previous credentials (if any) keep working.
        var password = PasswordGenerator.Generate();
        var baseUrl = config["Frontend:BaseUrl"] ?? "http://localhost:5173";
        try
        {
            await emailSender.SendAsync(
                student.Email,
                "Your VerBuddy account is ready",
                $"""
                 Hi {student.FirstName ?? student.DisplayName},

                 Your account for the VerBuddy has been activated.

                 Sign in at: {baseUrl}
                 Username:   {student.UserName}
                 Password:   {password}

                 You will be asked to choose your own password the first time you sign in.
                 """);
        }
        catch (Exception ex)
        {
            return $"The activation email could not be sent — check the SMTP settings. ({ex.Message})";
        }

        if (await userManager.HasPasswordAsync(student))
            await userManager.RemovePasswordAsync(student);
        var result = await userManager.AddPasswordAsync(student, password);
        if (!result.Succeeded)
            return string.Join(' ', result.Errors.Select(e => e.Description));

        student.MustChangePassword = true;
        student.ActivatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(student);

        return null;
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
