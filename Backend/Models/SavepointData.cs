using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

/// <summary>
/// Saved state of Savepoint, the owner's separate game-library app, which
/// uses VerBuddy for login and storage. One row per user (in practice only
/// the SuperAdmin). Savepoint treats <see cref="StateJson"/> as opaque JSON;
/// VerBuddy never reads into it.
/// </summary>
public class SavepointData
{
    /// <summary>The owning user's id. Deliberately no FK, like CreatedByAdminId.</summary>
    [Key, MaxLength(450)]
    public string UserId { get; set; } = null!;

    /// <summary>Bumped on every save; a save based on an older rev is refused (409).</summary>
    public int Rev { get; set; }

    /// <summary>Savepoint's own state-format version (for its migrations).</summary>
    public int Version { get; set; }

    public DateTime SavedAtUtc { get; set; }

    [Required]
    public string StateJson { get; set; } = null!;
}
