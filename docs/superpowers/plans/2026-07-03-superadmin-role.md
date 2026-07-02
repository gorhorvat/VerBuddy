# SuperAdmin Role + Admin Management Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `SuperAdmin` role (seeded from config) that alone can create/manage Admin accounts via new `api/superadmin/admins` endpoints and a frontend panel; rename roles `Teacher`→`Admin`, `Student`→`User`.

**Architecture:** ASP.NET Core Identity roles with role-list `[Authorize]` attributes. Account lifecycle (create-without-password → activate-with-emailed-password → forced change) is extracted into a shared `AccountLifecycleService` used by both `AdminStudentsController` and the new `AdminsController`. React frontend gets `isAdmin`/`isSuperAdmin` flags and a superadmin-only roster page.

**Tech Stack:** .NET 10 / ASP.NET Core, EF Core + SQL Server LocalDB, ASP.NET Identity, xUnit integration tests (`WebApplicationFactory`), React + TypeScript + Vite + Tailwind.

**Spec:** `docs/superpowers/specs/2026-07-03-superadmin-role-design.md`

## Global Constraints

- Role strings are exactly `SuperAdmin`, `Admin`, `User` (constants in `Backend/Models/AppRoles.cs`).
- PII (FirstName/LastName/Email) may only appear in DTOs returned from Admin/SuperAdmin-scoped controllers (existing GDPR boundary).
- The panel creates **Admins only** — never SuperAdmins.
- Seeded SuperAdmin credentials come from configuration keys `SuperAdmin:UserName`, `SuperAdmin:Email`, `SuperAdmin:Password`; if missing, seeding skips with a warning (app still runs).
- Existing role rename must preserve user→role assignments (in-place rename of the Identity role row, never delete+recreate).
- On activation, the credential email is sent FIRST; if delivery fails the account's password state is untouched.
- Backend tests: run from repo root with `dotnet test`. All existing suites must stay green.
- Frontend check: `npm run build` in `Frontend/` (tsc + vite) must pass.

---

### Task 0: Initialize git repository

The project folder is not a git repo (a `.gitignore` already exists at root). Plans require commits.

**Files:** none created by hand.

- [ ] **Step 1: Init and initial commit**

```bash
cd "c:/Users/gorho/SPM Interactive/.NET Projects/VerBuddy"
git init
git add -A
git commit -m "chore: initial commit of VerBuddy

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

Expected: commit created, working tree clean (`git status`).

---

### Task 1: Rename roles (Teacher→Admin, Student→User) end to end

**Files:**
- Modify: `Backend/Models/AppRoles.cs`
- Modify: `Backend/Data/DbSeeder.cs`
- Modify: `Backend/Controllers/AdminStudentsController.cs:23,34,215,254`
- Modify: `Backend/Controllers/AdminGamesController.cs:19`
- Modify: `Backend/Controllers/AdminCategoriesController.cs:18`
- Modify: `Backend/Controllers/AdminAttemptsController.cs:18`
- Modify: `Backend/Controllers/StudentGamesController.cs:20`
- Modify: `Backend/Controllers/LeaderboardController.cs:28`
- Modify: `Backend/Models/ApplicationUser.cs:10` (comment only)
- Modify: `Backend.Tests/AuthAndGdprTests.cs:17`

**Interfaces:**
- Produces: `AppRoles.SuperAdmin`, `AppRoles.Admin`, `AppRoles.User`, `AppRoles.AdminOrSuperAdmin` (`"Admin,SuperAdmin"`), `AppRoles.All` — every later task uses these constants.

- [ ] **Step 1: Update the existing role assertion to the new name (failing test)**

In `Backend.Tests/AuthAndGdprTests.cs` line 17, change:

```csharp
Assert.Contains("Teacher", auth.Roles);
```

to:

```csharp
Assert.Contains("Admin", auth.Roles);
```

- [ ] **Step 2: Run that test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~AuthAndGdpr"`
Expected: the login test FAILS (roles still contain "Teacher", not "Admin").

- [ ] **Step 3: Replace `Backend/Models/AppRoles.cs` entirely**

```csharp
namespace Backend.Models;

/// <summary>Role names used across authorization attributes and seeding.</summary>
public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string User = "User";

    /// <summary>For endpoints every admin (including SuperAdmin) may call.</summary>
    public const string AdminOrSuperAdmin = $"{Admin},{SuperAdmin}";

    public static readonly string[] All = [SuperAdmin, Admin, User];
}
```

- [ ] **Step 4: Add in-place legacy rename to `Backend/Data/DbSeeder.cs`**

Replace the `// ── Roles ──` block (and update the teacher role assignment below it) so `SeedAsync` reads:

```csharp
    public static async Task SeedAsync(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        await db.Database.MigrateAsync();

        // ── Legacy role rename (Teacher→Admin, Student→User) ──────────────
        // In-place rename keeps existing user-role assignments, which
        // reference the role Id, not its name.
        foreach (var (oldName, newName) in new[] { ("Teacher", AppRoles.Admin), ("Student", AppRoles.User) })
        {
            var legacy = await roleManager.FindByNameAsync(oldName);
            if (legacy is not null && await roleManager.FindByNameAsync(newName) is null)
            {
                await roleManager.SetRoleNameAsync(legacy, newName);
                await roleManager.UpdateAsync(legacy); // Persists Name + NormalizedName.
            }
        }

        // ── Roles ──────────────────────────────────────────────────────────
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ── Teacher account (PII fields populated — admin scope only) ─────
        var teacher = await userManager.FindByNameAsync("teacher.anna");
        if (teacher is null)
        {
            teacher = new ApplicationUser
            {
                UserName = "teacher.anna",
                Email = "anna@example-school.test",
                FirstName = "Anna",
                LastName = "Kovacs",
                DisplayName = "Ms. Anna",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(teacher, "ChangeMe!123");
        }
        if (!await userManager.IsInRoleAsync(teacher, AppRoles.Admin))
            await userManager.AddToRoleAsync(teacher, AppRoles.Admin);
    }
```

- [ ] **Step 5: Update every `AppRoles.Teacher` / `AppRoles.Student` reference**

Exact replacements (compiler will catch any missed one — the old constants no longer exist):

| File:line | Old | New |
|---|---|---|
| `Backend/Controllers/AdminStudentsController.cs:23` | `[Authorize(Roles = AppRoles.Teacher)]` | `[Authorize(Roles = AppRoles.AdminOrSuperAdmin)]` |
| `Backend/Controllers/AdminStudentsController.cs:34` | `GetUsersInRoleAsync(AppRoles.Student)` | `GetUsersInRoleAsync(AppRoles.User)` |
| `Backend/Controllers/AdminStudentsController.cs:215` | `IsInRoleAsync(user, AppRoles.Student)` | `IsInRoleAsync(user, AppRoles.User)` |
| `Backend/Controllers/AdminStudentsController.cs:254` | `AddToRoleAsync(student, AppRoles.Student)` | `AddToRoleAsync(student, AppRoles.User)` |
| `Backend/Controllers/AdminGamesController.cs:19` | `[Authorize(Roles = AppRoles.Teacher)]` | `[Authorize(Roles = AppRoles.AdminOrSuperAdmin)]` |
| `Backend/Controllers/AdminCategoriesController.cs:18` | `[Authorize(Roles = AppRoles.Teacher)]` | `[Authorize(Roles = AppRoles.AdminOrSuperAdmin)]` |
| `Backend/Controllers/AdminAttemptsController.cs:18` | `[Authorize(Roles = AppRoles.Teacher)]` | `[Authorize(Roles = AppRoles.AdminOrSuperAdmin)]` |
| `Backend/Controllers/StudentGamesController.cs:20` | `[Authorize(Roles = AppRoles.Student)]` | `[Authorize(Roles = AppRoles.User)]` |
| `Backend/Controllers/LeaderboardController.cs:28` | `GetUsersInRoleAsync(AppRoles.Student)` | `GetUsersInRoleAsync(AppRoles.User)` |

In `Backend/Models/ApplicationUser.cs:10` update the comment:

```csharp
///    ONLY be projected into admin DTOs and endpoints ([Authorize(Roles = "Admin,SuperAdmin")]).
```

- [ ] **Step 6: Run the full backend suite**

Run: `dotnet test`
Expected: ALL tests PASS. (The test DB is recreated per run — `EnsureDeletedAsync` in `ApiFactory.InitializeAsync` — so the rename path itself is exercised only against fresh DBs here; the dev DB rename is covered because `SeedAsync` runs the same code at app startup.)

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor: rename roles Teacher->Admin, Student->User; add SuperAdmin role constant

In-place Identity role rename in the seeder preserves existing
user-role assignments. Admin controllers now accept Admin or SuperAdmin.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: Seed SuperAdmin account from configuration

**Files:**
- Modify: `Backend/Data/DbSeeder.cs` (signature + new block)
- Modify: `Backend/Program.cs:79-84` (seeder call)
- Modify: `Backend/appsettings.Development.json`
- Modify: `Backend.Tests/TestInfra.cs` (config settings + seeder call)
- Modify: `Backend.Tests/TestHelpers.cs` (constants + client helper)
- Create: `Backend.Tests/AdminManagementTests.cs`

**Interfaces:**
- Consumes: `AppRoles.SuperAdmin` (Task 1).
- Produces: `DbSeeder.SeedAsync(AppDbContext, UserManager<ApplicationUser>, RoleManager<IdentityRole>, IConfiguration, ILogger? logger = null)`; test helpers `TestHelpers.SuperAdminUsername` (`"superadmin"`), `TestHelpers.SuperAdminPassword` (`"Super!Pass123"`), `factory.SuperAdminClientAsync()`.

- [ ] **Step 1: Add test helpers and the failing seed test**

In `Backend.Tests/TestHelpers.cs`, below `TeacherPassword`, add:

```csharp
    public const string SuperAdminUsername = "superadmin";
    public const string SuperAdminPassword = "Super!Pass123";
```

Below `TeacherClientAsync`, add:

```csharp
    public static async Task<HttpClient> SuperAdminClientAsync(this ApiFactory factory)
    {
        var auth = await factory.LoginAsync(SuperAdminUsername, SuperAdminPassword);
        return factory.ClientWithToken(auth.Token);
    }
```

Create `Backend.Tests/AdminManagementTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Backend.DTOs;
using Backend.Models;
using static Backend.Tests.TestHelpers;

namespace Backend.Tests;

[Collection("Api")]
public class AdminManagementTests(ApiFactory factory)
{
    [Fact]
    public async Task Seeded_superadmin_logs_in_with_superadmin_role()
    {
        var auth = await factory.LoginAsync(SuperAdminUsername, SuperAdminPassword);
        Assert.Contains(AppRoles.SuperAdmin, auth.Roles);
        Assert.DoesNotContain(AppRoles.Admin, auth.Roles);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~AdminManagementTests"`
Expected: FAIL — login returns 401 (`EnsureSuccessStatusCode` throws): no superadmin user exists.

- [ ] **Step 3: Extend `DbSeeder` with config-driven SuperAdmin**

Change the signature and add the block at the end of `SeedAsync`. New usings at top of `Backend/Data/DbSeeder.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
```

Signature:

```csharp
    public static async Task SeedAsync(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        ILogger? logger = null)
```

Append after the teacher block, inside `SeedAsync`:

```csharp
        // ── SuperAdmin (from configuration — never hardcoded) ─────────────
        var superUsername = configuration["SuperAdmin:UserName"];
        var superPassword = configuration["SuperAdmin:Password"];
        if (string.IsNullOrWhiteSpace(superUsername) || string.IsNullOrWhiteSpace(superPassword))
        {
            logger?.LogWarning(
                "SuperAdmin:UserName / SuperAdmin:Password are not configured — no SuperAdmin account was seeded.");
            return;
        }

        var super = await userManager.FindByNameAsync(superUsername);
        if (super is null)
        {
            super = new ApplicationUser
            {
                UserName = superUsername,
                Email = configuration["SuperAdmin:Email"],
                DisplayName = "Super Admin",
                EmailConfirmed = true
            };
            var created = await userManager.CreateAsync(super, superPassword);
            if (!created.Succeeded)
                throw new InvalidOperationException(
                    "SuperAdmin seeding failed: " + string.Join(' ', created.Errors.Select(e => e.Description)));
        }
        if (!await userManager.IsInRoleAsync(super, AppRoles.SuperAdmin))
            await userManager.AddToRoleAsync(super, AppRoles.SuperAdmin);
```

- [ ] **Step 4: Update the two seeder call sites**

`Backend/Program.cs` (Development block):

```csharp
    using var scope = app.Services.CreateScope();
    await DbSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<AppDbContext>(),
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
        scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
        app.Configuration,
        app.Logger);
```

`Backend.Tests/TestInfra.cs` — in `ConfigureWebHost`, after the `Frontend:BaseUrl` setting, add:

```csharp
        builder.UseSetting("SuperAdmin:UserName", "superadmin");
        builder.UseSetting("SuperAdmin:Email", "superadmin@test.local");
        builder.UseSetting("SuperAdmin:Password", "Super!Pass123");
```

In `InitializeAsync`, update the call (new using `Microsoft.Extensions.Configuration;` is already present):

```csharp
        await DbSeeder.SeedAsync( // Migrates, then seeds roles + accounts + sample game.
            db,
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
            scope.ServiceProvider.GetRequiredService<IConfiguration>());
```

- [ ] **Step 5: Add dev config**

In `Backend/appsettings.Development.json`, add a top-level section (after `"Frontend"`):

```json
  "SuperAdmin": {
    "UserName": "superadmin",
    "Email": "goran.horvat.apps@gmail.com",
    "Password": "ChangeMe!Super123"
  },
```

- [ ] **Step 6: Run tests**

Run: `dotnet test`
Expected: ALL PASS, including `Seeded_superadmin_logs_in_with_superadmin_role`.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: seed SuperAdmin account from configuration

Skips with a warning when SuperAdmin:UserName/Password are absent.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: Extract shared `AccountLifecycleService`

Pure refactor — the student endpoints keep identical behavior (same status codes, same email wording); the existing test suite is the safety net. This avoids copy-pasting ~100 lines of lifecycle logic into the new admins controller.

**Files:**
- Create: `Backend/Services/AccountLifecycleService.cs`
- Modify: `Backend/Program.cs` (register service)
- Modify: `Backend/Controllers/AdminStudentsController.cs` (consume service)

**Interfaces:**
- Consumes: `PasswordGenerator.Generate()`, `DisplayNameGenerator.Generate()`, `IEmailSender.SendAsync(to, subject, body)`.
- Produces (used by Task 4/5):
  - `Task<(ApplicationUser? User, (HttpStatusCode Status, string Message)? Error)> CreateAccountAsync(string role, string username, string? firstName, string? lastName, string? email, string? displayName, int? categoryId = null)`
  - `Task<string?> ActivateAsync(ApplicationUser user)` — null on success, else error message (maps to 400).
  - `Task<(HttpStatusCode Status, string Message)?> SendPasswordResetAsync(ApplicationUser user, string requestedByPhrase)` — null on success. Statuses used: `BadRequest` (no email), `Conflict` (deactivated), `BadGateway` (send failure).

- [ ] **Step 1: Create `Backend/Services/AccountLifecycleService.cs`**

```csharp
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
```

Note: the student reset email previously said "This student has no email address on file." / "This student is deactivated." — the generic "account" wording is an accepted, invisible-to-tests change (no test asserts those strings; verify with `grep -r "no email address" Backend.Tests` → no matches expected).

- [ ] **Step 2: Register in `Backend/Program.cs`**

After `builder.Services.AddScoped<ITokenService, TokenService>();` add:

```csharp
builder.Services.AddScoped<AccountLifecycleService>();
```

- [ ] **Step 3: Refactor `AdminStudentsController` to consume the service**

Constructor gains the service (replace the `IEmailSender emailSender, IConfiguration config` parameters — they are no longer used directly):

```csharp
public class AdminStudentsController(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    AccountLifecycleService lifecycle) : ControllerBase
```

Replace the private `CreateStudentAsync` body with delegation (category check stays here — it needs `db`):

```csharp
    private async Task<(ApplicationUser? Student, (HttpStatusCode Status, string Message)? Error)> CreateStudentAsync(
        string username, string? firstName, string? lastName,
        string? email, string? displayName, int? categoryId)
    {
        if (categoryId is not null && !await db.Categories.AnyAsync(c => c.Id == categoryId))
            return (null, (HttpStatusCode.BadRequest, "Unknown category."));

        return await lifecycle.CreateAccountAsync(
            AppRoles.User, username, firstName, lastName, email, displayName, categoryId);
    }
```

Replace the private `ActivateStudentAsync` entirely — delete it and change its two call sites (`Activate`, `ActivateBulk`) from `await ActivateStudentAsync(student)` to `await lifecycle.ActivateAsync(student)`.

Replace the body of `SendPasswordReset` (keep the route/signature):

```csharp
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
```

Remove now-unused usings if the compiler flags them (`Backend.Services` stays — `AccountLifecycleService` lives there).

- [ ] **Step 4: Run the full suite (behavioral parity check)**

Run: `dotnet test`
Expected: ALL PASS — especially `AccountFlowTests` (activation email password extraction, reset-link flow).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: extract AccountLifecycleService from AdminStudentsController

Create/activate/reset-password lifecycle becomes reusable for the
upcoming admin-account management endpoints.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 4: `AdminsController` — list, create, activate (+ access control)

**Files:**
- Create: `Backend/DTOs/AdminAccountDtos.cs`
- Create: `Backend/Controllers/AdminsController.cs`
- Modify: `Backend.Tests/AdminManagementTests.cs` (add tests)
- Modify: `Backend.Tests/TestHelpers.cs` (add `CreateActivatedAdminAsync`)

**Interfaces:**
- Consumes: `AccountLifecycleService` (Task 3 signatures), `AppRoles` (Task 1), `factory.SuperAdminClientAsync()` (Task 2).
- Produces:
  - DTOs: `AdminAccountDto(string Id, string Username, string? FirstName, string? LastName, string? Email, string DisplayName, bool IsActive, DateTime? ActivatedAt, bool MustChangePassword)`, `CreateAdminRequest(string Username, string? FirstName, string? LastName, string? Email, string? DisplayName)`.
  - Endpoints: `GET/POST api/superadmin/admins`, `POST api/superadmin/admins/{id}/activate`.
  - Test helper: `Task<TestAdmin> CreateActivatedAdminAsync(this ApiFactory factory, HttpClient superAdmin)` returning `record TestAdmin(string Id, string Username, string Password)`.

- [ ] **Step 1: Write failing tests**

Append to `Backend.Tests/AdminManagementTests.cs` (inside the class):

```csharp
    [Fact]
    public async Task Admin_and_anonymous_cannot_access_superadmin_endpoints()
    {
        using var admin = await factory.TeacherClientAsync();
        var forbidden = await admin.GetAsync("/api/superadmin/admins");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var anonymous = factory.CreateClient();
        var unauthorized = await anonymous.GetAsync("/api/superadmin/admins");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_creates_and_activates_admin_who_can_manage_students()
    {
        using var super = await factory.SuperAdminClientAsync();
        var created = await factory.CreateActivatedAdminAsync(super);

        // The new admin can call an Admin-scoped endpoint.
        var auth = await factory.LoginAsync(created.Username, created.Password);
        Assert.Contains(AppRoles.Admin, auth.Roles);
        using var adminClient = factory.ClientWithToken(auth.Token);
        var students = await adminClient.GetAsync("/api/admin/students");
        Assert.Equal(HttpStatusCode.OK, students.StatusCode);

        // And appears in the roster.
        var roster = await super.GetFromJsonAsync<List<AdminAccountDto>>("/api/superadmin/admins", Json);
        Assert.Contains(roster!, a => a.Id == created.Id);
    }

    [Fact]
    public async Task SuperAdmin_can_access_admin_scoped_endpoints()
    {
        using var super = await factory.SuperAdminClientAsync();
        var students = await super.GetAsync("/api/admin/students");
        Assert.Equal(HttpStatusCode.OK, students.StatusCode);
    }

    [Fact]
    public async Task Student_id_returns_404_on_admins_endpoints()
    {
        using var super = await factory.SuperAdminClientAsync();
        using var teacher = await factory.TeacherClientAsync();
        var student = await factory.CreateActivatedStudentAsync(teacher);

        var response = await super.PostAsync($"/api/superadmin/admins/{student.Id}/activate", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_display_name_returns_conflict()
    {
        using var super = await factory.SuperAdminClientAsync();
        var username = Unique("adm");
        var first = await super.PostAsJsonAsync("/api/superadmin/admins", new
        {
            username,
            firstName = "Dup",
            lastName = "Name",
            email = $"{username}@test.local",
            displayName = $"Dup-{username}"
        }, Json);
        first.EnsureSuccessStatusCode();

        var second = await super.PostAsJsonAsync("/api/superadmin/admins", new
        {
            username = Unique("adm"),
            firstName = "Dup",
            lastName = "Name",
            email = "other@test.local",
            displayName = $"Dup-{username}"
        }, Json);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
```

In `Backend.Tests/TestHelpers.cs`, add next to `TestStudent`:

```csharp
/// <summary>An activated admin ready to log in with a known password.</summary>
public sealed record TestAdmin(string Id, string Username, string Password);
```

And in the class, after `CreateActivatedStudentAsync`:

```csharp
    /// <summary>
    /// Admin account flow: create (no password) → activate (email captured) →
    /// first login with the temp password → change to a known password.
    /// </summary>
    public static async Task<TestAdmin> CreateActivatedAdminAsync(
        this ApiFactory factory, HttpClient superAdmin)
    {
        var username = Unique("adm");
        var email = $"{username}@test.local";

        var createResponse = await superAdmin.PostAsJsonAsync("/api/superadmin/admins", new
        {
            username,
            firstName = "Test",
            lastName = "Admin",
            email,
            displayName = (string?)null
        }, Json);
        createResponse.EnsureSuccessStatusCode();
        var dto = (await createResponse.Content.ReadFromJsonAsync<AdminAccountDto>(Json))!;

        var activateResponse = await superAdmin.PostAsync($"/api/superadmin/admins/{dto.Id}/activate", null);
        activateResponse.EnsureSuccessStatusCode();

        var tempPassword = ExtractPassword(factory.Emails.LastTo(email).Body);

        var firstLogin = await factory.LoginAsync(username, tempPassword);
        Assert.True(firstLogin.MustChangePassword);

        const string finalPassword = "Chosen!Admin123";
        using var client = factory.ClientWithToken(firstLogin.Token);
        var change = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = tempPassword, newPassword = finalPassword }, Json);
        change.EnsureSuccessStatusCode();

        return new TestAdmin(dto.Id, username, finalPassword);
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter "FullyQualifiedName~AdminManagementTests"`
Expected: FAIL to COMPILE (no `AdminAccountDto`) — that counts as the red step for endpoint tests. (If you want a runtime red instead: add the DTO file first, then routes 404.)

- [ ] **Step 3: Create `Backend/DTOs/AdminAccountDtos.cs`**

```csharp
using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs;

// ─── SuperAdmin payloads: admin-account PII is allowed here only. ─────────
// These must never be returned from endpoints reachable by Admin or User roles.

/// <summary>
/// No password field: credentials are generated server-side at activation time
/// and emailed to the admin, who must change them on first login.
/// </summary>
public sealed record CreateAdminRequest(
    [Required, MinLength(3), MaxLength(50)] string Username,
    [MaxLength(100)] string? FirstName,
    [MaxLength(100)] string? LastName,
    [EmailAddress, MaxLength(256)] string? Email,
    /// <summary>Optional nickname; a unique one is generated when omitted.</summary>
    [MaxLength(32)] string? DisplayName);

public sealed record UpdateAdminRequest(
    [MaxLength(100)] string? FirstName,
    [MaxLength(100)] string? LastName,
    [EmailAddress, MaxLength(256)] string? Email,
    [MaxLength(32)] string? DisplayName);

public sealed record AdminAccountDto(
    string Id,
    string Username,
    string? FirstName,
    string? LastName,
    string? Email,
    string DisplayName,
    bool IsActive,
    /// <summary>When the activation email was sent; null = never activated.</summary>
    DateTime? ActivatedAt,
    bool MustChangePassword);
```

- [ ] **Step 4: Create `Backend/Controllers/AdminsController.cs` (first three endpoints)**

```csharp
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
```

- [ ] **Step 5: Run tests**

Run: `dotnet test --filter "FullyQualifiedName~AdminManagementTests"`
Expected: ALL PASS.

- [ ] **Step 6: Run the full suite, then commit**

Run: `dotnet test` → ALL PASS.

```bash
git add -A
git commit -m "feat: SuperAdmin-only admin account creation and activation

New api/superadmin/admins endpoints (list/create/activate) reusing the
shared account lifecycle. Only SuperAdmin can create Admin accounts.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 5: `AdminsController` — edit, reset password, deactivate/reactivate, delete

**Files:**
- Modify: `Backend/Controllers/AdminsController.cs`
- Modify: `Backend.Tests/AdminManagementTests.cs`

**Interfaces:**
- Consumes: `FindAdminAsync`/`ToDto` (Task 4), `lifecycle.SendPasswordResetAsync(user, requestedByPhrase)` (Task 3), `UpdateAdminRequest` (Task 4 DTO file).
- Produces: `PUT api/superadmin/admins/{id}`, `POST {id}/reset-password`, `POST {id}/deactivate`, `POST {id}/reactivate`, `DELETE {id}`.

- [ ] **Step 1: Write failing tests**

Append to `Backend.Tests/AdminManagementTests.cs`:

```csharp
    [Fact]
    public async Task Update_edits_details_and_enforces_display_name_uniqueness()
    {
        using var super = await factory.SuperAdminClientAsync();
        var admin = await factory.CreateActivatedAdminAsync(super);
        var other = await factory.CreateActivatedAdminAsync(super);

        var updated = await super.PutAsJsonAsync($"/api/superadmin/admins/{admin.Id}", new
        {
            firstName = "New",
            lastName = "Name",
            email = "new.email@test.local",
            displayName = $"Renamed-{admin.Username}"
        }, Json);
        updated.EnsureSuccessStatusCode();
        var dto = (await updated.Content.ReadFromJsonAsync<AdminAccountDto>(Json))!;
        Assert.Equal("New", dto.FirstName);
        Assert.Equal("new.email@test.local", dto.Email);
        Assert.Equal($"Renamed-{admin.Username}", dto.DisplayName);

        // Taking the other admin's display name conflicts.
        var conflict = await super.PutAsJsonAsync($"/api/superadmin/admins/{other.Id}", new
        {
            firstName = "X",
            lastName = "Y",
            email = "x@test.local",
            displayName = $"Renamed-{admin.Username}"
        }, Json);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Reset_password_emails_link()
    {
        using var super = await factory.SuperAdminClientAsync();
        var admin = await factory.CreateActivatedAdminAsync(super);
        var email = $"{admin.Username}@test.local";

        var response = await super.PostAsync($"/api/superadmin/admins/{admin.Id}/reset-password", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains("/reset-password?user=", factory.Emails.LastTo(email).Body);
    }

    [Fact]
    public async Task Deactivate_blocks_login_and_reactivate_restores()
    {
        using var super = await factory.SuperAdminClientAsync();
        var admin = await factory.CreateActivatedAdminAsync(super);

        var deactivate = await super.PostAsync($"/api/superadmin/admins/{admin.Id}/deactivate", null);
        deactivate.EnsureSuccessStatusCode();

        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { username = admin.Username, password = admin.Password }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        var reactivate = await super.PostAsync($"/api/superadmin/admins/{admin.Id}/reactivate", null);
        reactivate.EnsureSuccessStatusCode();
        await factory.LoginAsync(admin.Username, admin.Password); // Throws if login fails.
    }

    [Fact]
    public async Task Delete_removes_account()
    {
        using var super = await factory.SuperAdminClientAsync();
        var admin = await factory.CreateActivatedAdminAsync(super);

        var delete = await super.DeleteAsync($"/api/superadmin/admins/{admin.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { username = admin.Username, password = admin.Password }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        var roster = await super.GetFromJsonAsync<List<AdminAccountDto>>("/api/superadmin/admins", Json);
        Assert.DoesNotContain(roster!, a => a.Id == admin.Id);
    }
```

- [ ] **Step 2: Run to verify failures**

Run: `dotnet test --filter "FullyQualifiedName~AdminManagementTests"`
Expected: the four new tests FAIL with 404/405 (routes don't exist); earlier tests still pass.

- [ ] **Step 3: Add the endpoints to `AdminsController` (before the Helpers section)**

```csharp
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

        await userManager.DeleteAsync(admin);
        return NoContent();
    }
```

- [ ] **Step 4: Run tests**

Run: `dotnet test --filter "FullyQualifiedName~AdminManagementTests"` → ALL PASS.
Then: `dotnet test` → ALL PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: admin account edit, reset, deactivate and delete endpoints

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 6: Frontend — role flags, superadmin panel page, routes and nav

**Files:**
- Modify: `Frontend/src/api.ts` (add `AdminAccount` type)
- Modify: `Frontend/src/auth.tsx` (`isTeacher` → `isAdmin` + `isSuperAdmin`)
- Modify: `Frontend/src/App.tsx` (routes)
- Modify: `Frontend/src/components/Layout.tsx` (nav tab + badge)
- Create: `Frontend/src/pages/superadmin/Admins.tsx`

**Interfaces:**
- Consumes: backend endpoints from Tasks 4-5; UI primitives `Button, Card, ErrorText, Spinner, inputClass` from `components/ui`; `ConfirmDialog` component.
- Produces: `useAuth()` now returns `{ user, isAdmin, isSuperAdmin, login, logout, clearMustChangePassword }` — `isTeacher` is gone; route `/superadmin/admins`.

- [ ] **Step 1: Add the `AdminAccount` type to `Frontend/src/api.ts`**

After the `StudentAdmin` interface add:

```ts
export interface AdminAccount {
  id: string
  username: string
  firstName: string | null
  lastName: string | null
  email: string | null
  displayName: string
  isActive: boolean
  activatedAt: string | null
  mustChangePassword: boolean
}
```

- [ ] **Step 2: Replace `isTeacher` in `Frontend/src/auth.tsx`**

In the `AuthContextValue` interface replace `isTeacher: boolean` with:

```ts
  /** True for Admin AND SuperAdmin — gates the admin (teacher) UI. */
  isAdmin: boolean
  isSuperAdmin: boolean
```

In the provider `value` replace the `isTeacher` line with:

```ts
        isAdmin: user?.roles.some((r) => r === 'Admin' || r === 'SuperAdmin') ?? false,
        isSuperAdmin: user?.roles.includes('SuperAdmin') ?? false,
```

- [ ] **Step 3: Update `Frontend/src/App.tsx`**

Add the import:

```ts
import Admins from './pages/superadmin/Admins'
```

Change the destructure and the teacher branch:

```tsx
  const { user, isAdmin, isSuperAdmin } = useAuth()
```

```tsx
        {isAdmin ? (
          <>
            <Route path="/teacher/games" element={<TeacherGames />} />
            <Route path="/teacher/games/:id" element={<GameEditor />} />
            <Route path="/teacher/games/:id/answers" element={<GameAnswers />} />
            <Route path="/teacher/reviews" element={<Reviews />} />
            <Route path="/teacher/students" element={<Students />} />
            {isSuperAdmin && <Route path="/superadmin/admins" element={<Admins />} />}
            <Route path="*" element={<Navigate to="/teacher/games" replace />} />
          </>
        ) : (
```

- [ ] **Step 4: Update `Frontend/src/components/Layout.tsx`**

```tsx
  const { user, isAdmin, isSuperAdmin, logout } = useAuth()

  const tabs = isAdmin
    ? [
        { to: '/teacher/games', label: 'Games', icon: '🎲' },
        { to: '/teacher/reviews', label: 'Reviews', icon: '📝' },
        { to: '/teacher/students', label: 'Students', icon: '👥' },
        ...(isSuperAdmin ? [{ to: '/superadmin/admins', label: 'Admins', icon: '🛡️' }] : []),
        { to: '/leaderboard', label: 'Ranks', icon: '🏆' },
      ]
    : [
        { to: '/games', label: 'Games', icon: '🎲' },
        { to: '/leaderboard', label: 'Ranks', icon: '🏆' },
      ]
```

And the header badge:

```tsx
            {isAdmin && (
              <span className="ml-2 rounded bg-indigo-100 px-1.5 py-0.5 text-xs">
                {isSuperAdmin ? 'Super Admin' : 'Admin'}
              </span>
            )}
```

- [ ] **Step 5: Create `Frontend/src/pages/superadmin/Admins.tsx`**

```tsx
import { useEffect, useState, type FormEvent } from 'react'
import { api, type AdminAccount } from '../../api'
import { Button, Card, ErrorText, Spinner, inputClass } from '../../components/ui'
import ConfirmDialog from '../../components/ConfirmDialog'

const emptyForm = { username: '', firstName: '', lastName: '', email: '', displayName: '' }

/**
 * SuperAdmin-only roster of admin (teacher) accounts. Same lifecycle as
 * students: create (no password) → Activate emails a temporary password →
 * the admin changes it on first login.
 */
export default function Admins() {
  const [admins, setAdmins] = useState<AdminAccount[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [showCreate, setShowCreate] = useState(false)
  const [deleteTarget, setDeleteTarget] = useState<AdminAccount | null>(null)
  const [editId, setEditId] = useState<string | null>(null)
  const [busyIds, setBusyIds] = useState<Set<string>>(new Set())

  const [form, setForm] = useState(emptyForm)
  const [editForm, setEditForm] = useState(emptyForm)

  const load = () =>
    api<AdminAccount[]>('/api/superadmin/admins').then(setAdmins).catch((e) => setError(e.message))

  useEffect(() => {
    load()
  }, [])

  const run = async (action: () => Promise<unknown>, successNotice?: string) => {
    setError(null)
    setNotice(null)
    try {
      await action()
      if (successNotice) setNotice(successNotice)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'The action failed.')
    }
  }

  const runFor = async (id: string, action: () => Promise<unknown>, successNotice?: string) => {
    setBusyIds((prev) => new Set(prev).add(id))
    await run(action, successNotice)
    setBusyIds((prev) => {
      const next = new Set(prev)
      next.delete(id)
      return next
    })
  }

  const set =
    (setter: typeof setForm) => (key: keyof typeof emptyForm) => (e: { target: { value: string } }) =>
      setter((f) => ({ ...f, [key]: e.target.value }))
  const setCreate = set(setForm)
  const setEdit = set(setEditForm)

  const create = (e: FormEvent) => {
    e.preventDefault()
    run(async () => {
      await api('/api/superadmin/admins', {
        method: 'POST',
        body: {
          username: form.username,
          firstName: form.firstName || null,
          lastName: form.lastName || null,
          email: form.email || null,
          displayName: form.displayName || null,
        },
      })
      setForm(emptyForm)
      setShowCreate(false)
    }, 'Admin created. Use "Activate" to email their first-login credentials.')
  }

  const startEdit = (a: AdminAccount) => {
    setEditId(a.id)
    setEditForm({
      username: a.username,
      firstName: a.firstName ?? '',
      lastName: a.lastName ?? '',
      email: a.email ?? '',
      displayName: a.displayName,
    })
  }

  const saveEdit = (e: FormEvent) => {
    e.preventDefault()
    const id = editId!
    run(async () => {
      await api(`/api/superadmin/admins/${id}`, {
        method: 'PUT',
        body: {
          firstName: editForm.firstName || null,
          lastName: editForm.lastName || null,
          email: editForm.email || null,
          displayName: editForm.displayName || null,
        },
      })
      setEditId(null)
    }, 'Admin updated.')
  }

  if (!admins) return <Spinner />

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-lg font-bold">🛡️ Admins</h1>
        <Button onClick={() => setShowCreate((v) => !v)} variant={showCreate ? 'secondary' : 'primary'}>
          {showCreate ? 'Cancel' : '+ Add admin'}
        </Button>
      </div>

      <ErrorText message={error} />
      {notice && <p className="rounded-lg bg-emerald-50 px-3 py-2 text-sm text-emerald-800">{notice}</p>}

      {showCreate && (
        <Card>
          <form onSubmit={create} className="space-y-3">
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <input className={inputClass} placeholder="Login username *" value={form.username} onChange={setCreate('username')} required minLength={3} maxLength={50} />
              <input className={inputClass} type="email" placeholder="Email (needed for activation)" value={form.email} onChange={setCreate('email')} />
              <input className={inputClass} placeholder="First name" value={form.firstName} onChange={setCreate('firstName')} />
              <input className={inputClass} placeholder="Last name" value={form.lastName} onChange={setCreate('lastName')} />
              <input className={inputClass} placeholder="Nickname (empty = auto-generate)" value={form.displayName} onChange={setCreate('displayName')} maxLength={32} />
            </div>
            <p className="text-xs text-slate-500">
              No password needed — activating the account emails a temporary password,
              which must be replaced on first login.
            </p>
            <Button type="submit" className="w-full">Create admin</Button>
          </form>
        </Card>
      )}

      {admins.map((a) => {
        const busy = busyIds.has(a.id)
        return (
          <Card key={a.id} className={`space-y-2 !py-3 ${!a.isActive ? 'opacity-60' : ''}`}>
            <div className="flex items-center gap-3">
              <div className="min-w-0 flex-1">
                <p className="font-semibold">
                  {a.firstName || a.lastName ? `${a.firstName ?? ''} ${a.lastName ?? ''}`.trim() : a.username}
                  <span className="ml-2 text-sm font-normal text-indigo-600">"{a.displayName}"</span>
                </p>
                <p className="truncate text-xs text-slate-500">
                  @{a.username}
                  {a.email && ` · ${a.email}`}
                </p>
              </div>
            </div>

            {editId === a.id ? (
              <form onSubmit={saveEdit} className="space-y-2">
                <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                  <input className={inputClass} placeholder="First name" value={editForm.firstName} onChange={setEdit('firstName')} />
                  <input className={inputClass} placeholder="Last name" value={editForm.lastName} onChange={setEdit('lastName')} />
                  <input className={inputClass} type="email" placeholder="Email" value={editForm.email} onChange={setEdit('email')} />
                  <input className={inputClass} placeholder="Nickname" value={editForm.displayName} onChange={setEdit('displayName')} maxLength={32} />
                </div>
                <div className="flex gap-2">
                  <Button type="submit" className="!px-3 !py-1 text-xs">Save</Button>
                  <Button type="button" variant="secondary" className="!px-3 !py-1 text-xs" onClick={() => setEditId(null)}>Cancel</Button>
                </div>
              </form>
            ) : (
              <div className="flex flex-wrap items-center gap-2">
                {!a.isActive ? (
                  <span className="rounded-full bg-rose-100 px-2.5 py-0.5 text-xs font-semibold text-rose-800">Deactivated</span>
                ) : a.activatedAt === null ? (
                  <span className="rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-semibold text-amber-800">Not activated</span>
                ) : a.mustChangePassword ? (
                  <span className="rounded-full bg-sky-100 px-2.5 py-0.5 text-xs font-semibold text-sky-800">Awaiting first login</span>
                ) : (
                  <span className="rounded-full bg-emerald-100 px-2.5 py-0.5 text-xs font-semibold text-emerald-800">Active</span>
                )}
                {a.isActive && (
                  <>
                    <Button
                      variant="secondary"
                      className="!px-3 !py-1 text-xs"
                      disabled={busy}
                      onClick={() => runFor(a.id, () => api(`/api/superadmin/admins/${a.id}/activate`, { method: 'POST' }), `Activation email sent to ${a.email}.`)}
                    >
                      ✉ {a.activatedAt ? 'Re-send activation' : 'Activate'}
                    </Button>
                    <Button
                      variant="secondary"
                      className="!px-3 !py-1 text-xs"
                      disabled={busy}
                      onClick={() => runFor(a.id, () => api(`/api/superadmin/admins/${a.id}/reset-password`, { method: 'POST' }), `Password reset link sent to ${a.email}.`)}
                    >
                      🔑 Reset password
                    </Button>
                    <Button
                      variant="secondary"
                      className="!px-3 !py-1 text-xs"
                      disabled={busy}
                      onClick={() => runFor(a.id, () => api(`/api/superadmin/admins/${a.id}/deactivate`, { method: 'POST' }))}
                    >
                      ⏸ Deactivate
                    </Button>
                  </>
                )}
                {!a.isActive && (
                  <Button
                    variant="secondary"
                    className="!px-3 !py-1 text-xs"
                    disabled={busy}
                    onClick={() => runFor(a.id, () => api(`/api/superadmin/admins/${a.id}/reactivate`, { method: 'POST' }))}
                  >
                    ▶ Reactivate
                  </Button>
                )}
                <Button variant="secondary" className="!px-3 !py-1 text-xs" disabled={busy} onClick={() => startEdit(a)}>
                  ✏️ Edit
                </Button>
                <Button variant="danger" className="!px-3 !py-1 text-xs" disabled={busy} onClick={() => setDeleteTarget(a)}>
                  🗑 Delete
                </Button>
              </div>
            )}
          </Card>
        )
      })}

      <ConfirmDialog
        open={deleteTarget !== null}
        title="Delete admin?"
        message={`"${deleteTarget?.username}" will be permanently deleted and will no longer be able to sign in.`}
        confirmLabel="Delete admin"
        onCancel={() => setDeleteTarget(null)}
        onConfirm={() => {
          const id = deleteTarget!.id
          setDeleteTarget(null)
          run(() => api(`/api/superadmin/admins/${id}`, { method: 'DELETE' }))
        }}
      />
    </div>
  )
}
```

- [ ] **Step 6: Type-check and build**

Run: `cd Frontend; npm run build`
Expected: tsc passes, vite build succeeds. A leftover `isTeacher` reference anywhere is a compile error — fix by using `isAdmin`.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: superadmin panel for managing admin accounts

isTeacher becomes isAdmin (true for Admin and SuperAdmin) plus
isSuperAdmin; new /superadmin/admins roster page and nav tab.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 7: Final verification

- [ ] **Step 1: Full backend suite**

Run: `dotnet test`
Expected: ALL PASS.

- [ ] **Step 2: Frontend build**

Run: `cd Frontend; npm run build`
Expected: success.

- [ ] **Step 3: Spec cross-check**

Confirm each spec section is implemented: role rename incl. in-place DB rename (Task 1), authorization matrix (Tasks 1+4), `api/superadmin/admins` endpoints (Tasks 4-5), config-driven seeding (Task 2), frontend flags/page/routes (Task 6), tests (Tasks 2, 4, 5).

- [ ] **Step 4: Manual smoke test (optional but recommended)**

Start backend (`dotnet run --project Backend`) and frontend (`cd Frontend; npm run dev`); log in as `superadmin` / `ChangeMe!Super123`; verify the Admins tab appears, create + activate an admin, check the pickup email/SMTP, log in as that admin and confirm the teacher UI works but the Admins tab is hidden.
