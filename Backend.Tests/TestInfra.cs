using Backend.Data;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Backend.Tests;

/// <summary>
/// Test double for the mail transport: captures every message in memory so
/// tests can read activation temp passwords and reset links.
/// </summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly List<(string To, string Subject, string Body)> _sent = [];

    public IReadOnlyList<(string To, string Subject, string Body)> Sent
    {
        get { lock (_sent) return _sent.ToList(); }
    }

    public Task SendAsync(string to, string subject, string body)
    {
        lock (_sent) _sent.Add((to, subject, body));
        return Task.CompletedTask;
    }

    public (string To, string Subject, string Body) LastTo(string email) =>
        Sent.Last(m => m.To == email);
}

/// <summary>
/// Boots the real API against a dedicated LocalDB test database
/// (VerBuddy_Tests), recreated from migrations once per test run and
/// seeded with the same dev seed (roles, teacher.anna, demo data).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public CapturingEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" skips Program.cs's Development-only migrate+seed block;
        // InitializeAsync below owns the database lifecycle instead.
        builder.UseEnvironment("Testing");

        // UseSetting (not ConfigureAppConfiguration) so the values are present
        // before Program.cs reads them during startup.
        builder.UseSetting("ConnectionStrings:DefaultConnection",
            "Server=(localdb)\\MSSQLLocalDB;Database=VerBuddy_Tests;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");
        builder.UseSetting("Jwt:Key", "tests-only-signing-key-0123456789abcdef0123456789abcdef0123456789abcdef");
        builder.UseSetting("Jwt:Issuer", "VerBuddy.Tests");
        builder.UseSetting("Jwt:Audience", "VerBuddy.Tests.Client");
        builder.UseSetting("Jwt:ExpiryMinutes", "60");
        builder.UseSetting("Frontend:BaseUrl", "http://localhost:5173");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await DbSeeder.SeedAsync( // Migrates, then seeds roles + accounts + sample game.
            db,
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    /// <summary>Direct database access for test arrangement (e.g. backdating attempts).</summary>
    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}

/// <summary>Single collection so all API tests share one database, run serially.</summary>
[CollectionDefinition("Api")]
public class ApiCollection : ICollectionFixture<ApiFactory>;
