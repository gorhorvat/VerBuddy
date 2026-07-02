# SuperAdmin Role + Admin Management Panel — Design

**Date:** 2026-07-03
**Status:** Approved (design discussion in session)

## Goal

Add a `SuperAdmin` role that is the only role allowed to create and manage admin
(teacher) accounts, plus a super-admin panel in the frontend. Broader granular
rights for SuperAdmin are a later phase; for now SuperAdmin has full access to
everything an Admin has, plus admin-account management.

As part of this work, roles are renamed because the app will be used outside
the school system:

| Old | New |
|-----|-----|
| Teacher | Admin |
| Student | User |
| — | SuperAdmin (new) |

UI wording ("teacher", "student") may stay where it reads naturally; only the
**role strings** are renamed.

## Decisions (from brainstorming)

- New admin accounts use the **same lifecycle as students**: create without
  password → activate → random password emailed → `MustChangePassword` on
  first login.
- SuperAdmin **gets all Admin access now** (combined role list on existing
  controllers), granular rights later.
- The panel creates **Admins only** — no new SuperAdmins from the UI. The
  single SuperAdmin is seeded.
- Seeded SuperAdmin comes **from configuration** (`SuperAdmin:UserName`,
  `SuperAdmin:Email`, `SuperAdmin:Password`), not hardcoded.
- Management actions on admin accounts: deactivate/reactivate, password-reset
  email, edit details (name/email/display name), hard delete.
- Authorization approach: **role lists on attributes** (Approach A), not
  policies. Migrate to policies when the rights matrix grows.

## 1. Roles

`Backend/Models/AppRoles.cs`:

```csharp
public const string SuperAdmin = "SuperAdmin";
public const string Admin = "Admin";      // was Teacher
public const string User = "User";        // was Student
public const string AdminOrSuperAdmin = "Admin,SuperAdmin";
public static readonly string[] All = [SuperAdmin, Admin, User];
```

**DB rename:** `DbSeeder` renames existing Identity roles in place before
ensuring roles exist: if a role named `Teacher` exists, update its `Name` and
`NormalizedName` to `Admin`; same for `Student` → `User`. Idempotent; keeps
all existing user-role assignments (they reference role Id, not name).

## 2. Authorization changes

| Controller | Attribute |
|-----------|-----------|
| AdminStudentsController | `[Authorize(Roles = AppRoles.AdminOrSuperAdmin)]` |
| AdminGamesController | `[Authorize(Roles = AppRoles.AdminOrSuperAdmin)]` |
| AdminCategoriesController | `[Authorize(Roles = AppRoles.AdminOrSuperAdmin)]` |
| AdminAttemptsController | `[Authorize(Roles = AppRoles.AdminOrSuperAdmin)]` |
| StudentGamesController | `[Authorize(Roles = AppRoles.User)]` |
| **AdminsController (new)** | `[Authorize(Roles = AppRoles.SuperAdmin)]` |

All other `AppRoles.Teacher` / `AppRoles.Student` references (role checks,
`GetUsersInRoleAsync`, `AddToRoleAsync`, seeder, tests) update to the new
constants.

## 3. New controller: `AdminsController`

Route: `api/superadmin/admins`. SuperAdmin only. Mirrors the student
lifecycle in `AdminStudentsController` (same `PasswordGenerator`,
`IEmailSender`, `MustChangePassword` mechanics).

| Endpoint | Behavior |
|----------|----------|
| `GET /` | List all Admin-role accounts with PII (`AdminAccountDto`). |
| `POST /` | Create admin: username, first/last name, email, optional display name. No password. DisplayName generated if omitted, uniqueness enforced (same rules as students). |
| `POST /{id}/activate` | Generate random password, email credentials (email sent first — account untouched on failure), set `MustChangePassword`, set `ActivatedAt`. |
| `POST /{id}/reset-password` | Email single-use reset link (same flow/template as students). |
| `POST /{id}/deactivate` | `IsActive = false`; login blocked. |
| `POST /{id}/reactivate` | `IsActive = true`. |
| `PUT /{id}` | Update FirstName, LastName, Email, DisplayName (display-name uniqueness enforced). |
| `DELETE /{id}` | Hard delete. Safe: no ownership FKs from games/categories/attempts to admin users. |

Lookups scoped to Admin-role users only (a SuperAdmin or User id returns 404),
mirroring `FindStudentAsync`.

New DTO file `Backend/DTOs/AdminAccountDtos.cs`: `AdminAccountDto`,
`CreateAdminRequest`, `UpdateAdminRequest`. PII allowed here — this controller
is SuperAdmin-scoped, consistent with the existing GDPR boundary rule
(PII only behind admin-scoped endpoints).

## 4. SuperAdmin seeding

`DbSeeder` reads `SuperAdmin:UserName`, `SuperAdmin:Email`,
`SuperAdmin:Password` from configuration:

- If the user doesn't exist: create with `EmailConfirmed = true`, assign
  `SuperAdmin` role.
- If config section missing/incomplete: skip creation and log a warning
  (app still runs; no superadmin until configured).
- Dev values live in `appsettings.Development.json`; production uses
  environment variables (`SuperAdmin__Password` etc.).
- Existing `teacher.anna` dev seed stays, now assigned the `Admin` role.

## 5. Frontend

- `auth.tsx`: replace `isTeacher` with `isAdmin`
  (`roles` includes `Admin` **or** `SuperAdmin`) and add `isSuperAdmin`
  (`roles` includes `SuperAdmin`). Update all consumers.
- New page `src/pages/superadmin/Admins.tsx`, modeled on
  `pages/teacher/Students.tsx`: roster table (name, username, email, display
  name, status), create form, and per-row actions: activate, resend
  activation, reset password, deactivate/reactivate, edit, delete
  (with `ConfirmDialog`).
- New API functions in `api.ts` for the `api/superadmin/admins` endpoints.
- Route `/superadmin/admins`; nav link rendered only when `isSuperAdmin`.
- SuperAdmin sees all existing teacher pages too (backend already permits;
  frontend gates them on `isAdmin`, which includes SuperAdmin).

## 6. Testing

- Update `TestHelpers`/existing tests for renamed role strings; all existing
  suites must pass unchanged in behavior.
- New tests (`AdminManagementTests`):
  - Admin (teacher) calling `api/superadmin/admins` → 403; unauthenticated → 401.
  - SuperAdmin can create → activate → new admin logs in with emailed
    password → forced password change flag present.
  - Created admin can access admin endpoints (e.g. list students).
  - Edit updates fields; duplicate display name → 409.
  - Deactivate blocks login; reactivate restores.
  - Delete removes account; deleted admin login fails.
  - SuperAdmin can access existing admin endpoints (combined role check).

## Out of scope (later phases)

- Granular SuperAdmin rights matrix / policy-based authorization.
- Creating additional SuperAdmins from the UI.
- Audit logging of admin-management actions.
