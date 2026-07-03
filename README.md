<p align="center">
  <img src="Frontend/public/verbuddy-logo-transparent.png" alt="VerBuddy" width="220" />
</p>

# VerBuddy

**Live app: [verbuddy.vercel.app](https://verbuddy.vercel.app)**

VerBuddy is a gamified English-learning platform for schools. Teachers build
word games — single/multiple choice, fill-in-the-blanks, word matching — and
file them into classes. Students play them, earn XP, level up, climb the
leaderboards, and redeem real-world rewards their teacher defines.

## Features

**For students**
- Play vocabulary games with optional time limits; instant scoring or manual
  teacher review
- XP and leveling (level 1 at 1,000 XP, every next level costs double), with
  a level badge and progress bar in the header and a level-up celebration
- Per-class and global leaderboards under pseudonymous nicknames
- Rewards catalog: see everything, unlock by level, apply — teacher approves
  or denies

**For teachers (admins)**
- Game builder with per-type question editing, draft/active/closed lifecycle,
  duplicate-as-template
- Class (category) management; students can belong to several classes and see
  exactly the games of their classes
- Student roster with provisioning lifecycle: create without password →
  activation email with temporary credentials → forced password change on
  first login; CSV bulk import; bulk activation
- Manual grading queue for feedback-required games with score overrides
- Rewards management and approval/revocation of student reward requests
- Teachers see only the students and rewards they created

**For super admins**
- Admin account management (create, activate, edit, deactivate, delete) with
  the same email-based activation lifecycle
- Full visibility across all teachers' students, categories, and rewards

**Privacy by design** — student real names and emails are visible only to
staff; classmates and leaderboards ever see just the chosen nickname.

## Tech stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core (.NET 10), Entity Framework Core, ASP.NET Core Identity, JWT bearer auth |
| Database | SQL Server (Azure SQL in production, LocalDB in development) |
| Frontend | React 19, TypeScript, Vite, Tailwind CSS v4, React Router |
| Email | SMTP via MailKit (Gmail/Brevo/any relay); file-pickup transport in development |
| Tests | xUnit integration suite against a real database via `WebApplicationFactory` |
| Hosting | Frontend on Vercel · API on Azure App Service · CI/CD with GitHub Actions |

## Repository layout

```
Backend/          ASP.NET Core API (controllers, EF Core models & migrations, services)
Backend.Tests/    xUnit integration tests (real HTTP + real SQL, no mocks)
Frontend/         React SPA (Vite + Tailwind)
docs/             Deployment guide and design documents
```

## Running locally

Prerequisites: .NET 10 SDK, Node 20+, SQL Server LocalDB.

```bash
# API — applies migrations and seeds demo data on first start
dotnet run --project Backend        # http://localhost:5247

# Frontend
cd Frontend
npm install
npm run dev                         # http://localhost:5173
```

Development seed accounts (`Backend/appsettings.Development.json`):
demo teacher `teacher.anna` and a configurable super admin. The demo account
is only seeded when `Seed:DemoAccounts` is `true` — never in production.

Run the test suite from the repo root:

```bash
dotnet test
```

## Deployment

Frontend deploys to Vercel from `Frontend/`; the API deploys to Azure App
Service via GitHub Actions with an Azure SQL serverless database. The full
step-by-step guide, including all required environment variables, lives in
[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).
