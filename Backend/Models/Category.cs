namespace Backend.Models;

/// <summary>
/// A teacher-defined folder/class (e.g. "5th Grade A"). Games are filed into a
/// category and students may belong to any number of them (their classes, used
/// for per-class leaderboards). Uncategorized items render under a virtual
/// "General" group.
/// </summary>
public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string TeacherId { get; set; } = null!;
    public ApplicationUser Teacher { get; set; } = null!;

    public ICollection<GameInstance> Games { get; set; } = new List<GameInstance>();
    public ICollection<ApplicationUser> Students { get; set; } = new List<ApplicationUser>();
}
