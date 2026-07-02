using System.Security.Cryptography;

namespace Backend.Services;

/// <summary>
/// Generates random first-login passwords for student activation. Students are
/// forced to replace them on first login (MustChangePassword), so these are
/// only ever short-lived credentials.
/// </summary>
public static class PasswordGenerator
{
    private const string Lower = "abcdefghjkmnpqrstuvwxyz";
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";
    private const string Symbols = "!#$%&*+-?";

    public static string Generate(int length = 12)
    {
        // One of each class guarantees the Identity password policy is met.
        var chars = new List<char>
        {
            Pick(Lower), Pick(Upper), Pick(Digits), Pick(Symbols)
        };
        const string all = Lower + Upper + Digits + Symbols;
        while (chars.Count < length)
            chars.Add(Pick(all));

        // Shuffle so the guaranteed classes aren't always in front.
        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars.ToArray());
    }

    private static char Pick(string alphabet) =>
        alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
