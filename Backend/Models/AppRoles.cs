namespace Backend.Models;

/// <summary>Role names used across authorization attributes and seeding.</summary>
public static class AppRoles
{
    public const string Teacher = "Teacher";
    public const string Student = "Student";

    public static readonly string[] All = [Teacher, Student];
}
