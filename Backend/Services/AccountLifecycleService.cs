using System.Net;
using Backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>
/// Shared account lifecycle for provisioned accounts (students and admins):
/// create (no password) → activate (random password emailed, MustChangePassword
/// set) → password-reset links. On activation the email goes out FIRST; if
/// delivery fails the account's password state is left untouched.
/// </summary>
public sealed class AccountLifecycleService(
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    IConfiguration config)
{
    private string FrontendBaseUrl => config["Frontend:BaseUrl"] ?? "http://localhost:5173";

    /// <summary>
    /// Creates a password-less account in the given role. DisplayName is
    /// generated when omitted; uniqueness is enforced either way.
    /// </summary>
    public async Task<(ApplicationUser? User, (HttpStatusCode Status, string Message)? Error)> CreateAccountAsync(
        string role, string username, string? firstName, string? lastName,
        string? email, string? displayName, int? categoryId = null)
    {
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

        var user = new ApplicationUser
        {
            UserName = username.Trim(),
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            DisplayName = displayName,
            CategoryId = categoryId,
            EmailConfirmed = true // No email round-trip for provisioned accounts.
        };

        // No password: the account is unusable until activated.
        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
            return (null, (HttpStatusCode.BadRequest, string.Join(' ', result.Errors.Select(e => e.Description))));

        await userManager.AddToRoleAsync(user, role);
        return (user, null);
    }

    /// <summary>Returns an error message, or null on success.</summary>
    public async Task<string?> ActivateAsync(ApplicationUser user)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
            return "No email address on file — add one before activating.";
        if (!user.IsActive)
            return "The account is deactivated.";

        // A fresh password is generated at activation (and on re-activation
        // emails), so plaintext credentials never need to be stored.
        var password = PasswordGenerator.Generate();
        try
        {
            await emailSender.SendAsync(
                user.Email,
                "Your VerBuddy account is ready",
                $"""
                 Hi {user.FirstName ?? user.DisplayName},

                 Your account for the VerBuddy has been activated.

                 Sign in at: {FrontendBaseUrl}
                 Username:   {user.UserName}
                 Password:   {password}

                 You will be asked to choose your own password the first time you sign in.
                 """);
        }
        catch (Exception ex)
        {
            return $"The activation email could not be sent — check the SMTP settings. ({ex.Message})";
        }

        if (await userManager.HasPasswordAsync(user))
            await userManager.RemovePasswordAsync(user);
        var result = await userManager.AddPasswordAsync(user, password);
        if (!result.Succeeded)
            return string.Join(' ', result.Errors.Select(e => e.Description));

        user.MustChangePassword = true;
        user.ActivatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        return null;
    }

    /// <summary>
    /// Emails a single-use reset link. Returns null on success, or a status +
    /// message the controller maps onto the HTTP response.
    /// </summary>
    public async Task<(HttpStatusCode Status, string Message)?> SendPasswordResetAsync(
        ApplicationUser user, string requestedByPhrase)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
            return (HttpStatusCode.BadRequest, "This account has no email address on file.");
        if (!user.IsActive)
            return (HttpStatusCode.Conflict, "This account is deactivated.");

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var link = $"{FrontendBaseUrl}/reset-password?user={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}";

        try
        {
            await emailSender.SendAsync(
                user.Email,
                "Reset your VerBuddy password",
                $"""
                 Hi {user.FirstName ?? user.DisplayName},

                 {requestedByPhrase} requested a password reset for your account ({user.UserName}).
                 Open this link to choose a new password:

                 {link}

                 If you didn't expect this email you can ignore it.
                 """);
        }
        catch (Exception ex)
        {
            return (HttpStatusCode.BadGateway,
                $"The reset email could not be sent — check the SMTP settings. ({ex.Message})");
        }

        return null;
    }
}
