namespace Backend.Models;

/// <summary>Role names used across authorization attributes and seeding.</summary>
public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string User = "User";

    /// <summary>For endpoints every admin (including SuperAdmin) may call.</summary>
    public const string AdminOrSuperAdmin = $"{Admin},{SuperAdmin}";

    public static readonly string[] All = [SuperAdmin, Admin, User];
}
