using System.Security.Claims;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService) : ControllerBase
{
    /// <summary>
    /// Username + password login for both roles. Returns a JWT whose claims are
    /// pseudonymous only. Failures are deliberately indistinguishable
    /// (unknown user, wrong password, locked out, deactivated) to prevent enumeration.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByNameAsync(request.Username);
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
            return Unauthorized(new { message = "Invalid username or password." });

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user); // Counts toward lockout.
            return Unauthorized(new { message = "Invalid username or password." });
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        var (token, expiresAtUtc) = tokenService.CreateToken(user, roles);

        return new AuthResponse(
            token, expiresAtUtc, user.DisplayName, user.TotalXp, roles, user.MustChangePassword);
    }

    /// <summary>
    /// The caller's own profile. Even for self, only pseudonymous fields are
    /// returned — PII stays behind the teacher-admin endpoints.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var user = await FindCallerAsync();
        if (user is null)
            return Unauthorized();

        var roles = await userManager.GetRolesAsync(user);
        return new MeResponse(user.UserName!, user.DisplayName, user.TotalXp, roles);
    }

    /// <summary>
    /// Self-service password change; completing it clears the first-login
    /// MustChangePassword flag set at activation.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await FindCallerAsync();
        if (user is null)
            return Unauthorized();

        var result = await userManager.ChangePasswordAsync(
            user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        user.MustChangePassword = false;
        await userManager.UpdateAsync(user);
        return NoContent();
    }

    /// <summary>
    /// Completes the reset flow started by the teacher's "Reset Password" button:
    /// the emailed link carries the user id and a single-use Identity reset token.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null || !user.IsActive)
            return BadRequest(new { message = "This reset link is not valid." });

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { message = "This reset link is not valid or has expired." });

        user.MustChangePassword = false;
        await userManager.UpdateAsync(user);
        return NoContent();
    }

    private async Task<ApplicationUser?> FindCallerAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null ? null : await userManager.FindByIdAsync(userId);
    }
}
