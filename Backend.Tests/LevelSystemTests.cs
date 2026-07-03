using Backend.Services;

namespace Backend.Tests;

public class LevelSystemTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(999, 0)]
    [InlineData(1000, 1)]
    [InlineData(2999, 1)]
    [InlineData(3000, 2)]
    [InlineData(7000, 3)]
    public void LevelForXp_MatchesKnownThresholds(int totalXp, int expectedLevel)
    {
        Assert.Equal(expectedLevel, LevelSystem.LevelForXp(totalXp));
    }

    [Fact]
    public void LevelForXp_NegativeXp_IsLevelZero()
    {
        Assert.Equal(0, LevelSystem.LevelForXp(-500));
    }

    /// <summary>
    /// The exponential cost curve means int.MaxValue (~2.1B, the ceiling of the
    /// int-typed TotalXp column) never gets anywhere near level 99 — the
    /// threshold for level 22 alone already exceeds int range. This test
    /// documents that ceiling and proves the safety clamp never overshoots it.
    /// </summary>
    [Fact]
    public void LevelForXp_AtIntMaxValue_IsBoundedAndNeverExceedsMaxLevel()
    {
        var level = LevelSystem.LevelForXp(int.MaxValue);

        Assert.InRange(level, 0, LevelSystem.MaxLevel);
        Assert.Equal(21, level); // exact ceiling reachable via a 32-bit XP total
        Assert.True(LevelSystem.ThresholdFor(level) <= int.MaxValue);
        Assert.True(LevelSystem.ThresholdFor(level + 1) > int.MaxValue);
    }

    [Fact]
    public void ThresholdFor_Level0_IsZero()
    {
        Assert.Equal(0, LevelSystem.ThresholdFor(0));
    }

    [Theory]
    [InlineData(1, 1000)]
    [InlineData(2, 3000)]
    [InlineData(3, 7000)]
    public void ThresholdFor_MatchesGeometricSum(int level, long expectedThreshold)
    {
        Assert.Equal(expectedThreshold, LevelSystem.ThresholdFor(level));
    }

    /// <summary>
    /// Level 99 requires computing (or safely saturating) 1000 * (2^99 - 1),
    /// which overflows even a 64-bit long. Must not throw and must stay positive.
    /// </summary>
    [Fact]
    public void ThresholdFor_Level99_DoesNotOverflowOrThrow()
    {
        var threshold = LevelSystem.ThresholdFor(99);
        Assert.True(threshold > 0);
    }

    [Fact]
    public void ThresholdFor_ClampsLevelsAboveMax()
    {
        Assert.Equal(LevelSystem.ThresholdFor(99), LevelSystem.ThresholdFor(150));
    }
}
