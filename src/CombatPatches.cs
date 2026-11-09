using HarmonyLib;
using Il2CppReloaded.Gameplay;

namespace DifficultyMod;

[HarmonyPatch(typeof(Zombie), nameof(Zombie.TakeDamage))]
internal static class DamagePatch
{
    // Attacker and resistance patches can replace the damage value entirely.
    // Apply receiver difficulty last, after Fusion's final conversion (-100),
    // exactly once before native shield/helmet/body distribution.
    [HarmonyPriority(int.MinValue)]
    private static void Prefix(Zombie __instance, ref int theDamage)
    {
        var mod = Mod.Instance;
        if (!mod.Owns(__instance)) return;
        int incoming = theDamage;
        theDamage = DifficultyRules.IncomingDamage(mod.Active, incoming, __instance.mMindControlled);
        mod.ReportDamage(__instance, incoming, theDamage);
    }
}

[HarmonyPatch(typeof(Zombie), nameof(Zombie.PickRandomSpeed))]
internal static class SpeedPatch
{
    private static void Postfix(Zombie __instance)
    {
        var mod = Mod.Instance;
        if (mod.Owns(__instance) && mod.Active == Difficulty.Extreme && !__instance.mMindControlled &&
            !__instance.mApp.IsIZombieLevel()) __instance.mVelX += .1f;
    }
}

[HarmonyPatch(typeof(Zombie), nameof(Zombie.StartMindControlled))]
internal static class HypnosisPatch
{
    private static bool Prefix(Zombie __instance)
    {
        var mod = Mod.Instance;
        return !mod.Owns(__instance) || mod.Active != Difficulty.Extreme || __instance.mMindControlled ||
            UnityEngine.Random.Range(0, 2) == 0;
    }
}

[HarmonyPatch(typeof(Zombie), nameof(Zombie.ApplyBurn))]
internal static class BurnPatch
{
    private static bool Prefix(Zombie __instance)
    {
        var mod = Mod.Instance;
        if (!mod.Owns(__instance) || __instance.mMindControlled ||
            !DifficultyRules.ResistsBurn(mod.Active, __instance.mBodyHealth + __instance.mHelmHealth)) return true;
        __instance.TakeDamage(1800, DamageFlags.BypassesShield | DamageFlags.DoesntLeaveBody);
        return false;
    }
}

// PickZombieWaves assigns its budget AFTER ZombiePickerInitForWave. Scale on
// the first PickZombieType call for each wave, before the native picker consumes
// any of that budget. Preserve the native 50-zombie capacity and type eligibility.
[HarmonyPatch(typeof(Board), nameof(Board.PickZombieWaves))]
internal static class WaveStartPatch
{
    internal static readonly HashSet<(IntPtr Board, int Wave)> Scaled = new();
    private static void Prefix(Board __instance) => Scaled.RemoveWhere(item => item.Board == __instance.Pointer);
    private static void Postfix(Board __instance) => Scaled.RemoveWhere(item => item.Board == __instance.Pointer);
}

[HarmonyPatch(typeof(Board), nameof(Board.PickZombieType))]
internal static class WaveBudgetPatch
{
    private static void Prefix(Board __instance, ref int theZombiePoints, int theWaveIndex, Board.ZombiePicker theZombiePicker)
    {
        var mod = Mod.Instance;
        if (!mod.Owns(__instance) || !WaveStartPatch.Scaled.Add((__instance.Pointer, theWaveIndex))) return;
        int budget = DifficultyRules.WaveBudget(mod.Active, theZombiePoints);
        theZombiePoints = theZombiePicker.ZombiePoints = budget;
    }
}
