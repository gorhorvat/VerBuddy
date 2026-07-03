# Deploying VerBuddy

Architecture: **frontend on Vercel** (static Vite build), **backend + SQL Server on Azure**
(App Service + Azure SQL). Vercel cannot host ASP.NET Core or SQL Server.

## 1. Azure SQL (database)

1. Portal → *Create resource* → **SQL Database** (free serverless offer: 32 GB, auto-pause).
   - New server: pick region, **SQL authentication**, note admin login + password.
   - Database name: `VerBuddy`.
2. Server → *Networking* → allow Azure services + add your own IP for management.
3. Copy the ADO.NET connection string (Settings → Connection strings). It looks like:
   `Server=tcp:<server>.database.windows.net,1433;Initial Catalog=VerBuddy;User ID=<login>;Password=<pw>;Encrypt=True;`

Schema + data: nothing to do — the backend applies EF migrations and seeds
roles/SuperAdmin on startup. To carry over local data instead, use SSMS →
right-click DB → *Deploy Database to Microsoft Azure SQL Database* before first start.

## 2. Azure App Service (backend)

1. *Create resource* → **Web App**: Runtime **.NET 10**, OS Linux, Free (F1) plan.
2. Deploy code — simplest is GitHub: push this repo to GitHub, then Web App →
   *Deployment Center* → GitHub → select repo/branch. Set the build to the
   `Backend` project (the generated workflow: `dotnet publish Backend/Backend.csproj`).
   Alternatively from a terminal: `dotnet publish Backend -c Release -o pub` then
   `az webapp deploy --resource-group <rg> --name <app> --src-path pub --type zip` (zip the folder).
3. Web App → *Settings → Environment variables* — add (double underscore = section separator):

   | Name | Value |
   |---|---|
   | `ConnectionStrings__DefaultConnection` | the Azure SQL connection string |
   | `Jwt__Key` | long random secret (64+ chars, generate fresh) |
   | `Jwt__Issuer` | `VerBuddy` |
   | `Jwt__Audience` | `VerBuddy.Client` |
   | `Jwt__ExpiryMinutes` | `480` |
   | `SuperAdmin__UserName` | your superadmin login |
   | `SuperAdmin__Email` | your email |
   | `SuperAdmin__Password` | strong password (used only at first seed) |
   | `Frontend__BaseUrl` | `https://<your-app>.vercel.app` (used in credential emails) |
   | `Cors__Origins` | `https://<your-app>.vercel.app` (semicolon-separate to add more) |
   | `Email__Smtp__Host` | `smtp-relay.brevo.com` |
   | `Email__Smtp__Port` | `587` |
   | `Email__Smtp__Username` | Brevo SMTP login |
   | `Email__Smtp__Password` | Brevo SMTP key |
   | `Email__Smtp__From` | sender address verified in Brevo |
   | `Email__Smtp__FromName` | `VerBuddy` |

   Do NOT set `Seed__DemoAccounts` in production — the demo `teacher.anna`
   account (known password) is seeded only where that flag is `true` (dev/tests).
4. Restart the app. First start runs migrations + seeds roles and the SuperAdmin.
   Check *Log stream* if it fails (most common: SQL firewall or connection string).

## 3. Vercel (frontend)

1. vercel.com → *Add New Project* → import the repo.
   - **Root Directory:** `Frontend`
   - Framework preset: Vite (build `npm run build`, output `dist` — auto-detected).
2. Project → *Settings → Environment Variables*:
   - `VITE_API_URL` = `https://<your-app>.azurewebsites.net` (no trailing slash)
3. Deploy. SPA routing is handled by `Frontend/vercel.json` (rewrite all → index.html).
4. If the Vercel domain changes (custom domain), update `Cors__Origins` and
   `Frontend__BaseUrl` on the App Service.

## 4. Smoke test

1. `https://<app>.azurewebsites.net/api/auth/login` responds (405/400 on GET is fine).
2. Open the Vercel URL → log in as the configured SuperAdmin → create + activate an
   admin (email arrives via Brevo) → log in as that admin → create a class, students, game.

## Local development (unchanged)

LocalDB + `appsettings.Development.json` (`Seed:DemoAccounts: true` seeds
`teacher.anna` / `ChangeMe!123`; SuperAdmin from the same file). CORS defaults
to `http://localhost:5173` when `Cors:Origins` is absent.
