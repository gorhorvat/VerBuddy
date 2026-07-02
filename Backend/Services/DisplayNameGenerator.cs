using System.Security.Cryptography;

namespace Backend.Services;

/// <summary>
/// Generates pseudonymous, kid-friendly display names ("BraveOtter42") used when
/// the teacher doesn't assign a nickname explicitly at account creation.
/// Uniqueness is enforced by the caller against the DisplayName unique index.
/// </summary>
public static class DisplayNameGenerator
{
    private static readonly string[] Adjectives =
    [
        "Brave", "Clever", "Swift", "Mighty", "Sunny", "Lucky",
        "Gentle", "Bold", "Merry", "Quiet", "Witty", "Nimble"
    ];

    private static readonly string[] Animals =
    [
        "Otter", "Falcon", "Panda", "Tiger", "Dolphin", "Koala",
        "Lynx", "Badger", "Heron", "Fox", "Wolf", "Owl"
    ];

    public static string Generate()
    {
        var adjective = Adjectives[RandomNumberGenerator.GetInt32(Adjectives.Length)];
        var animal = Animals[RandomNumberGenerator.GetInt32(Animals.Length)];
        var number = RandomNumberGenerator.GetInt32(10, 100);
        return $"{adjective}{animal}{number}";
    }
}
