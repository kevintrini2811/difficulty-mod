namespace DifficultyMod;

// Fusion 4.0.5.2: DifficultyMgr, InitZombieList and Zombie are the reference.
// Normal is the third option in that reference, despite its default being 2.
public enum Difficulty { Easy = 1, Medium = 2, Normal = 3, Harder = 4, Extreme = 5 }

internal static class DifficultyRules
{
    internal static bool Valid(int value) => value is >= 1 and <= 5;
    internal static Difficulty Validate(int value) => Valid(value) ? (Difficulty)value : Difficulty.Normal;
    internal static int IncomingDamage(Difficulty difficulty, int damage, bool friendly)
    {
        if (friendly || damage <= 0) return damage;
        return difficulty switch
        {
            Difficulty.Easy => damage > int.MaxValue - 10 ? int.MaxValue : damage + 10,
            Difficulty.Extreme => damage / 2,
            _ => damage
        };
    }

    // Retain Replanted's level-specific budget and normalize Fusion's 1:2:3:4:4
    // progression to Normal. Extreme uses 4 on every level, including 1-1/1-2.
    internal static int WaveBudget(Difficulty difficulty, int original)
    {
        int multiplier = difficulty == Difficulty.Extreme ? 4 : (int)difficulty;
        return Math.Max(1, (int)Math.Min(int.MaxValue, ((long)original * multiplier + 1) / 3));
    }

    internal static bool ResistsBurn(Difficulty difficulty, int bodyAndHelmet) =>
        difficulty == Difficulty.Harder && bodyAndHelmet > 1800 ||
        difficulty == Difficulty.Extreme && bodyAndHelmet > 900;
}
