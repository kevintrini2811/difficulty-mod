using System.Security.Cryptography;
using BloomEngine.Helpers;
using BloomEngine.ModMenu;
using HarmonyLib;
using Il2CppReloaded.Data;
using Il2CppReloaded.DataModels;
using Il2CppReloaded.Gameplay;
using Il2CppReloaded.Services;
using Il2CppReloaded.TreeStateActivities;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

[assembly: MelonInfo(typeof(DifficultyMod.Mod), "Difficulty Mod", "1.0.6", "Kevin / Codex")]
[assembly: MelonGame("PopCap Games", "PvZ Replanted")]
[assembly: MelonOptionalDependencies("DebugMod")]

namespace DifficultyMod;

public sealed class Mod : MelonMod
{
    internal static Mod Instance = null!;
    internal ProgressStore Progress = null!;
    internal NativeUi Ui = null!;
    internal MelonPreferences_Entry<int> Default = null!;
    internal MelonPreferences_Category Settings = null!;
    internal Difficulty Active = Difficulty.Normal;
    internal GameplayActivity? App;
    internal string? RunKey;
    internal string? PendingKey;
    internal Difficulty PendingDifficulty;
    internal bool InRun;
    internal bool Launching;
    internal bool ApiAdjustedRun;

    internal static void SetApiDifficulty(Board board, int value)
    {
        if (value is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(value));
        var mod = Instance;
        if (!mod.Owns(board)) throw new InvalidOperationException("Difficulty Mod does not own this board.");
        mod.Active = (Difficulty)value;
        mod.ApiAdjustedRun = true;
    }
    private float nextScan;
    private Sprite? menuIcon;
    private int damageSamples;

    public override void OnInitializeMelon()
    {
        Instance = this;
        Settings = MelonPreferences.CreateCategory("DifficultyMod", "Difficulty Mod");
        Settings.SetFilePath(Path.Combine(MelonEnvironment.UserDataDirectory, "DifficultyMod.cfg"));
        Default = Settings.CreateEntry("DefaultDifficulty", 3, "Default difficulty",
            "1 Easy (blue), 2 Medium (silver), 3 Normal (orange), 4 Harder (gold), 5 Extreme (black). Applies to new level selections.");
        Progress = new ProgressStore(Path.Combine(MelonEnvironment.UserDataDirectory, "DifficultyMod", "progress.json"),
            message => LoggerInstance.Error(message));
        Ui = new NativeUi(this);
        // Like Plant Library, embed lawn and foreground in one opaque square.
        // BloomEngine owns placement and border; no background is added to text.
        menuIcon = AssetHelper.LoadSprite<Mod>("DifficultyMod.Assets.menu-icon.png");
        menuIcon.texture.hideFlags = HideFlags.HideAndDontSave;
        ModMenuService.CreateEntry(this).AddIcon(menuIcon)
            .AddDescription("Five difficulty levels with right-click selection and trophies for completed levels.")
            .Register();
        LoggerInstance.Msg("Difficulty Mod ready. Right-click a level for Fusion difficulty; default is Normal (3).");
    }

    internal string? Profile => App?.UserService?.ActiveUserProfile?.mGuid ?? Ui.UserService?.ActiveUserProfile?.mGuid;
    internal static string LevelKey(LevelEntryData level) =>
        $"{(int)level.GameMode}:{(int)level.ReloadedGameMode}:{level.LevelNumber}:{level.SubIndex}:{level.name}";
    internal string? Key(LevelEntryData? level) => level == null || string.IsNullOrEmpty(Profile)
        ? null : Profile + "/" + LevelKey(level);

    internal void Select(LevelEntryModel model, Difficulty difficulty)
    {
        PendingKey = Key(model.EntryData);
        PendingDifficulty = difficulty;
    }

    internal void Begin(GameplayActivity app)
    {
        ApiAdjustedRun = false;
        App = app;
        string? key = Key(app.LevelData ?? app.GameplayService.CurrentLevelData);
        if (key == null) { InRun = false; RunKey = null; return; }
        if (PendingKey == key)
        {
            Active = PendingDifficulty;
            PendingKey = null;
        }
        else if (!InRun) Active = DifficultyRules.Validate(Default.Value);
        RunKey = key; InRun = true;
        damageSamples = 0;
        LoggerInstance.Msg($"Level {key}: {Active}");
    }

    internal bool Owns(Board? board) => InRun && board != null && App?.Pointer == board.mApp?.Pointer;
    internal bool Owns(Zombie zombie) => Owns(zombie.mBoard);

    internal void ReportDamage(Zombie zombie, int incoming, int applied)
    {
        // Keep enough evidence for the user's manual check without logging
        // every projectile or changing any combat state for diagnostics.
        if (Active != Difficulty.Extreme || incoming == applied || damageSamples >= 5) return;
        damageSamples++;
        LoggerInstance.Msg($"Extreme damage: incoming={incoming}, applied={applied}, zombie={zombie.mZombieType}.");
    }

    internal static string? Fingerprint(SerializationService serializer)
    {
        int category = (int)serializer.GetCurrentSaveCategory();
        if (category < 0 || category >= serializer.m_saveBuffer.Count || !serializer.BufferIsValid(category)) return null;
        var buffer = serializer.m_saveBuffer[category];
        int length = serializer.BufferTotalSize(category);
        if (length <= 0 || length > buffer.Length) return null;
        return Convert.ToHexString(SHA256.HashData(buffer.AsSpan()[..length]));
    }

    internal bool MayLoad(SerializationService serializer)
    {
        if (!InRun || RunKey == null || serializer.App?.Pointer != App?.Pointer) return true;
        string? fingerprint = Fingerprint(serializer);
        if (fingerprint == null) return true;
        // Pre-mod or externally changed saves have no provable mod difficulty.
        // They can only resume at Normal; other choices always start fresh.
        Difficulty saved = Progress.Saved(RunKey, fingerprint) ?? Difficulty.Normal;
        if (saved == Active) return true;
        LoggerInstance.Msg($"Saved difficulty {saved} differs from {Active}; starting this level again before seed selection.");
        return false;
    }

    public override void OnUpdate()
    {
        Ui.Update();
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + .5f;
        Ui.Scan();
    }

    public override void OnDeinitializeMelon()
    {
        Ui.Dispose();
        if (menuIcon != null)
        {
            var texture = menuIcon.texture;
            UnityEngine.Object.Destroy(menuIcon);
            UnityEngine.Object.Destroy(texture);
            menuIcon = null;
        }
    }
}

[HarmonyPatch(typeof(GameplayActivity), nameof(GameplayActivity.NewGame))]
internal static class NewGamePatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(GameplayActivity __instance) => Mod.Instance.Begin(__instance);
}

[HarmonyPatch(typeof(GameplayActivity), nameof(GameplayActivity.InactiveStarted))]
internal static class LeaveGamePatch
{
    private static void Postfix()
    {
        var mod = Mod.Instance;
        mod.InRun = false; mod.RunKey = null; mod.PendingKey = null;
    }
}

[HarmonyPatch(typeof(SerializationService), nameof(SerializationService.Deserialize))]
internal static class LoadPatch
{
    private static bool Prefix(SerializationService __instance, ref bool __result)
    {
        if (Mod.Instance.MayLoad(__instance)) return true;
        __result = false; return false;
    }
}

[HarmonyPatch(typeof(SerializationService), nameof(SerializationService.Serialize))]
internal static class SavePatch
{
    private static void Prefix(SerializationService __instance, out string? __state)
    {
        var mod = Mod.Instance;
        __state = mod.InRun && __instance.App?.Pointer == mod.App?.Pointer ? Mod.Fingerprint(__instance) : null;
    }
    private static void Postfix(SerializationService __instance, string? __state)
    {
        var mod = Mod.Instance;
        if (!mod.InRun || mod.RunKey == null || __instance.App?.Pointer != mod.App?.Pointer) return;
        var fingerprint = Mod.Fingerprint(__instance);
        // Serialize has early returns during seed selection and after a loss.
        // Never relabel an older native save when no new bytes were written.
        if (fingerprint != null && fingerprint != __state) mod.Progress.Save(mod.RunKey, mod.Active, fingerprint);
    }
}

[HarmonyPatch(typeof(GameplayActivity), nameof(GameplayActivity.UpdatePlayerProfileForFinishingLevel))]
internal static class WinPatch
{
    private static void Prefix(GameplayActivity __instance)
    {
        var mod = Mod.Instance;
        if (mod.InRun && !mod.ApiAdjustedRun && mod.RunKey != null && mod.App?.Pointer == __instance.Pointer &&
            __instance.Board?.mLevelComplete == true && __instance.BoardResult != BoardResult.Cheat)
            mod.Progress.Win(mod.RunKey, mod.Active);
    }
}
