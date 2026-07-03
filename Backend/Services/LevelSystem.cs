namespace Backend.Services;

/// <summary>
/// Shared XP → level math for the leveling/rewards system. Cost to go from
/// level n-1 to level n is 1000 * 2^(n-1) XP (doubles every level), so the
/// cumulative XP needed to REACH level n is the geometric sum 1000*(2^n - 1).
/// Level 0 sits at 0 XP; level is capped at <see cref="MaxLevel"/>.
///
/// Note: because XP is stored as a 32-bit <c>int</c> (see ApplicationUser.TotalXp,
/// max ~2.1B), the exponential curve makes level ~21 the practical ceiling —
/// the threshold for level 22 alone already exceeds int.MaxValue. MaxLevel=99
/// and the overflow-safe clamp in <see cref="ThresholdFor"/> exist purely as a
/// defensive cap; they are not reachable via ordinary gameplay XP.
/// </summary>
public static class LevelSystem
{
    public const int MaxLevel = 99;

    /// <summary>
    /// Largest level for which 1000 * (2^n - 1) still fits in a signed 64-bit
    /// long without overflowing (2^54 * 1000 would overflow; 2^53 does not).
    /// </summary>
    private const int SafeExponent = 53;

    /// <summary>Cumulative XP required to reach <paramref name="level"/> (0 for level 0 or below).</summary>
    public static long ThresholdFor(int level)
    {
        var n = Math.Clamp(level, 0, MaxLevel);
        if (n == 0)
            return 0;

        // 2^99 overflows even a long; levels beyond the safe exponent are
        // saturated rather than wrapped, since no int-typed XP total could
        // ever reach them anyway.
        if (n > SafeExponent)
            return long.MaxValue;

        return 1000L * ((1L << n) - 1);
    }

    /// <summary>Highest level (0..MaxLevel) whose cumulative threshold the XP total meets.</summary>
    public static int LevelForXp(int totalXp)
    {
        if (totalXp <= 0)
            return 0;

        var level = 0;
        for (var n = 1; n <= MaxLevel; n++)
        {
            if (ThresholdFor(n) > totalXp)
                break;
            level = n;
        }
        return level;
    }
}
