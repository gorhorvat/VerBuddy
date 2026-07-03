using Backend.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<GameInstance> GameInstances => Set<GameInstance>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<StudentAttempt> StudentAttempts => Set<StudentAttempt>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Reward> Rewards => Set<Reward>();
    public DbSet<RewardApplication> RewardApplications => Set<RewardApplication>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Identity tables first.

        builder.Entity<ApplicationUser>(user =>
        {
            user.Property(u => u.FirstName).HasMaxLength(100);
            user.Property(u => u.LastName).HasMaxLength(100);

            user.Property(u => u.DisplayName).IsRequired().HasMaxLength(32);
            // DisplayName is the public identity — must be unique classroom-wide.
            user.HasIndex(u => u.DisplayName).IsUnique();

            // Leaderboard reads sort by TotalXp.
            user.HasIndex(u => u.TotalXp);

            // A student may belong to any number of classes (categories), via an
            // explicit join table so both FKs can cascade-delete cleanly.
            user.HasMany(u => u.Categories)
                .WithMany(c => c.Students)
                .UsingEntity<Dictionary<string, object>>(
                    "StudentCategories",
                    j => j.HasOne<Category>().WithMany()
                        .HasForeignKey("CategoryId")
                        .OnDelete(DeleteBehavior.Cascade),
                    j => j.HasOne<ApplicationUser>().WithMany()
                        .HasForeignKey("StudentId")
                        .OnDelete(DeleteBehavior.Cascade),
                    j =>
                    {
                        j.HasKey("StudentId", "CategoryId");
                        j.ToTable("StudentCategories");
                    });
        });

        builder.Entity<Category>(category =>
        {
            category.Property(c => c.Name).IsRequired().HasMaxLength(100);
            category.HasIndex(c => new { c.TeacherId, c.Name }).IsUnique();

            // Restrict avoids a cascade cycle with the student CategoryId SetNull.
            category.HasOne(c => c.Teacher)
                .WithMany()
                .HasForeignKey(c => c.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GameInstance>(game =>
        {
            game.Property(g => g.Title).IsRequired().HasMaxLength(200);
            game.Property(g => g.Description).HasMaxLength(1000);

            // Stored as readable strings so the DB is self-describing for the teacher.
            game.Property(g => g.GameType).HasConversion<string>().HasMaxLength(30);
            game.Property(g => g.State).HasConversion<string>().HasMaxLength(20);

            // Student dashboard query: "all Active games".
            game.HasIndex(g => g.State);

            game.HasOne(g => g.CreatedByTeacher)
                .WithMany(u => u.CreatedGames)
                .HasForeignKey(g => g.CreatedByTeacherId)
                .OnDelete(DeleteBehavior.Restrict);

            // Deleting a category moves its games back to "General" (null).
            game.HasOne(g => g.Category)
                .WithMany(c => c.Games)
                .HasForeignKey(g => g.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Question>(question =>
        {
            question.Property(q => q.Prompt).IsRequired().HasMaxLength(2000);

            // Variable per-game-type payload — nvarchar(max) JSON keeps the
            // schema stable when new game types are added.
            question.Property(q => q.JsonContent)
                .IsRequired()
                .HasColumnType("nvarchar(max)");

            question.HasOne(q => q.GameInstance)
                .WithMany(g => g.Questions)
                .HasForeignKey(q => q.GameInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            question.HasIndex(q => new { q.GameInstanceId, q.Order });
        });

        builder.Entity<StudentAttempt>(attempt =>
        {
            attempt.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            attempt.Property(a => a.AnswersJson).HasColumnType("nvarchar(max)");
            attempt.Property(a => a.TeacherFeedback).HasMaxLength(2000);

            // The one-attempt lock: a student can never hold two attempts
            // for the same game instance.
            attempt.HasIndex(a => new { a.GameInstanceId, a.StudentId }).IsUnique();

            attempt.HasOne(a => a.GameInstance)
                .WithMany(g => g.Attempts)
                .HasForeignKey(a => a.GameInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict avoids multiple cascade paths (SQL Server limitation) and
            // ensures a user delete is an explicit, audited GDPR erasure flow.
            attempt.HasOne(a => a.Student)
                .WithMany(u => u.Attempts)
                .HasForeignKey(a => a.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Reward>(reward =>
        {
            reward.Property(r => r.Title).IsRequired().HasMaxLength(200);
            reward.Property(r => r.Description).HasMaxLength(1000);
        });

        builder.Entity<RewardApplication>(application =>
        {
            application.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

            // Deleting a reward removes every application filed against it.
            application.HasOne(a => a.Reward)
                .WithMany(r => r.Applications)
                .HasForeignKey(a => a.RewardId)
                .OnDelete(DeleteBehavior.Cascade);

            // Deleting a student erases their applications too (GDPR erasure).
            application.HasOne(a => a.Student)
                .WithMany()
                .HasForeignKey(a => a.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            application.HasIndex(a => new { a.StudentId, a.RewardId });
        });
    }
}
