using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

/// <summary>Decision state of a student's application for a reward.</summary>
public enum RewardApplicationStatus
{
    Pending = 0,
    Approved = 1,
    Denied = 2
}

/// <summary>
/// A global, teacher-defined reward unlocked once a student reaches
/// <see cref="RequiredLevel"/>. Visible to every student (not filed by class).
/// </summary>
public class Reward
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = null!;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>0..99 — the level (see <see cref="Services.LevelSystem"/>) a student must reach to unlock this.</summary>
    [Range(0, 99)]
    public int RequiredLevel { get; set; }

    public ICollection<RewardApplication> Applications { get; set; } = new List<RewardApplication>();
}

/// <summary>
/// A student's request to redeem a reward they've unlocked. A student may have
/// at most one non-Denied application per reward at a time (enforced in the
/// controller, not the schema); a Denied application may be re-applied for,
/// creating a new row.
/// </summary>
public class RewardApplication
{
    public int Id { get; set; }

    public int RewardId { get; set; }
    public Reward Reward { get; set; } = null!;

    public string StudentId { get; set; } = null!;
    public ApplicationUser Student { get; set; } = null!;

    public RewardApplicationStatus Status { get; set; } = RewardApplicationStatus.Pending;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAtUtc { get; set; }
}
