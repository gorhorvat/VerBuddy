namespace Backend.Models;

/// <summary>
/// A teacher-defined grouping (e.g. "5th Grade A"). Assignments are filed into a
/// category and students may belong to any number of them (used for
/// per-category leaderboards). Uncategorized items render under a virtual
/// "General" group.
/// </summary>
public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string TeacherId { get; set; } = null!;
    public ApplicationUser Teacher { get; set; } = null!;

    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    public ICollection<ApplicationUser> Students { get; set; } = new List<ApplicationUser>();
}
