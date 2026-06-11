namespace DCBot.Services.Level;

/// <summary>
/// Pure XP math. Curve: XP needed to go from level L to L+1
/// = 5·L² + 50·L + 100 (MEE6-style). Max level 1000.
/// </summary>
public static class XpMath
{
    public const int MaxLevel = 1000;

    /// <summary>XP needed to advance FROM this level to the next one.</summary>
    public static long XpForLevel(int level)
        => 5L * level * level + 50L * level + 100L;

    /// <summary>Total XP required to HAVE reached the given level.</summary>
    public static long TotalXpForLevel(int level)
    {
        long total = 0;
        for (var l = 0; l < level && l < MaxLevel; l++)
            total += XpForLevel(l);
        return total;
    }

    /// <summary>Level for a given total XP amount.</summary>
    public static int CalcLevel(long totalXp)
    {
        var level = 0;
        long needed = 0;
        while (level < MaxLevel)
        {
            needed += XpForLevel(level);
            if (totalXp < needed) break;
            level++;
        }
        return level;
    }

    /// <summary>XP already earned inside the current level.</summary>
    public static long XpIntoCurrentLevel(long totalXp)
        => totalXp - TotalXpForLevel(CalcLevel(totalXp));
}
