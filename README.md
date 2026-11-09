# Difficulty Mod 1.0.6

## Dependencies

- Game/runtime: PvZ Replanted (Windows/IL2CPP), MelonLoader 0.7.3, and .NET 6.
- Required mods: `BloomEngine.dll`.
- Optional integrations: `DebugMod`.
- Building: .NET SDK with `net6.0` support and the game/MelonLoader reference assemblies declared in `DifficultyMod.csproj`.

## Debug-API in 1.0.6

Debug Mod 1.3.0 can change the difficulty of the current level through the responsible difficulty mod path. Saved default selections and existing wins remain unchanged. A run that is adjusted via API during the current level will not receive a Difficulty Cup. An actual restarted run resets this status.

MelonLoader mod for the installed Windows/IL2CPP version of **PvZ Replanted**. The source code is entirely in this folder. The mod uses the existing MelonLoader installation and the already installed BloomEngine for its entry in the mod menu. ReplantAPI, FusionMod and LawnGlove will not be changed.

## Mod menu in 1.0.4

The icon shows the gold match cup on the same native turf as Plant Library. As with their `tools/export_menu_assets.py`, the background and subject are embedded in an opaque image with 512 × 512 pixels. BloomEngine receives this image via `AddIcon` and takes the position and frame. The lawn remains limited to the icon area.

The description reads: “Five difficulty levels with right-click selection and trophies for levels won.” As with Plant Library, it is registered via `AddDescription` and replaces the general author/version display.

`tools/build_menu_icon.py` uses the existing `plant-library/assets/lawn.png` and `assets/trophy.png`. A local lawn copy and `assets/menu-icon-source.json` document the sources. The finished `assets/menu-icon.png` is embedded; Plant Library is not required for the icon at runtime. Difficulty rules, settings and progression will not be adjusted by this change.

## Extreme in 1.0.3

Extreme now also uses the multiplier 4 on adventures 1-1 and 1-2. The previous introductory exceptions are no longer applicable. Relative to normal, a rounded wave budget of four thirds applies at every level.

The damage change runs at the end of the `Zombie.TakeDamage` prefixes after other installed mods have calculated their attack, bonus and resistance values. Previously, the halving ran first and could be overwritten by later damage replacements. The halving occurs once before the native distribution on the shield, helmet and body. For example, 20 final attack damage points at Extreme become 10. Hypnotized, friendly zombies are excluded.

The first five changed hits of an extreme level are logged in the MelonLoader log with `Extreme damage: incoming=..., applied=..., zombie=...`. This limited diagnostic supports manual testing. Existing wins and configuration will be retained. A pass that has already been saved contains its old waves; Restart the level for the new wave values.

## Presentation and language in 1.0.2

The labels on the five selection cards are in their own area at a distance from the lower stone frame. The cup has a separate area above it. Centering, single line, and automatic font adjustment replace the text dimensions and original baseline alignment provided for larger original maps.

The mod uses `ISettingsService.CurrentLanguage`, the same game value that the native `LocalizationActivity` uses. German shows “Easy,” “Medium,” “Normal,” “Heavy,” and “Extreme”; English shows “Easy”, “Medium”, “Normal”, “Harder” and “Extreme”. Heading, Cancel and the settings button also change with the game language. Language changes are also applied when the mod window is open. For other languages, English texts will initially be used.

In the BloomEngine mod menu, the mod uses the unchanged golden game cup from `assets/trophy.png`. Registration takes place via `ModMenuService.CreateEntry(...).AddIcon(...)`; This eliminates the standard image with the vase. The difficulty settings and existing victories remain unchanged.

## Fix in 1.0.1

Version 1.0.0 incorrectly used the mini-game menu filename as the addressables key. The game reported `InvalidKeyException`. As a result, both the selection and the cup display originally linked to it were deactivated. The reported normal win at 1-1 was already correctly saved in the progress file.

Version 1.0.1 copies the already loaded native `PanelView` with ID `minigames`. The actual card comes from the native `ListBinder` serialized template, not from possibly renamed or recycled list entries. Trophies are loaded and displayed regardless of the availability of the selection window. Level cards are assigned via the resolved model context of their own binder. The progress file is not changed by the installation.

## Controls

- **Left click / normal confirmation:** Start with the default difficulty set, initially **Normal**.
- **Right click on an unlocked level map or its start button:** Before the actual level start, a customized copy of the native mini-game menu opens. Five cards each show the colored cup and the name of the difficulty.
- **Cancel / Escape:** Closes the selection without starting the level and without changing the difficulty.
- **Automatic follow-up level or restart within a current run:** Maintains the difficulty of the run.
- **Options, “Difficulty”:** Opens the same selection for the default difficulty. Changes apply to the next new level selection; a current level will not be changed subsequently.

The default difficulty can also be set in `UserData/DifficultyMod.cfg` under `[DifficultyMod]`, `DefaultDifficulty`. Changes outside of the game occur when the game is closed.

## Difficulties and trophies

The order and English names come from the local Fusion reference `pvzfusion/source-expanded-unofficial/Assets/PVZFUSIONMOD/Assets/Scripts/Managers/DifficultyMgr.cs`:

1. **Easy:** Blau.
2. **Medium:** Silber.
3. **Normal:** Orange.
4. **Harder:** Gold.
5. **Extreme:** Black.

Each supported level map shows the highest difficulty gained with that mod in the bottom left. Levels that have not yet been won will not receive a trophy. Lower wins do not replace a higher trophy. Existing native completions will not be subsequently issued as mod victories.

The representation uses the existing game asset `SPR_Challenges_Trophy`, the native mini-game menu cup. `assets/trophy.png` contains its unchanged export. The mod neutralizes the golden base color at runtime and then colors the surface. It does not create a new cup drawing and does not change any original files. Adventure maps, the older level view as well as mini-games, puzzles, survival and other maps with the native level map templates will be integrated. Locked levels remain locked.

## Transfer of Fusion Rules

The reference uses five levels and wave multipliers **1:2:3:4:4**. These ratios are carried over to Replanted: **Normal retains Replanted's native wave budget**, the remaining stages receive one third, two thirds and four thirds respectively. The introductory exceptions of the Fusion reference for Extreme have not been adopted since 1.0.3 at the express request of the user. Rounding is done to whole budget points. The native capacity limit of 50 zombies per wave and the native selection of allowed zombie types remain.

Additional rules from `Zombie.cs` of the Fusion Reference:

- Easy: An enemy zombie takes 10 additional damage per positive hit.
- Medium and Normal: No additional damage change.
- Harder: With more than 1800 health from body and helmet, a burn attack deals 1800 damage instead of instant burning.
- Extreme: Enemy zombies receive positive damage halved, rounded down to an integer. Random running speed increases by 0.1 native units, except in I-Zombie. Hypnosis has a 50% chance of success. With more than 900 health points from body and helmet, immediate burning is replaced by an attack with 1800 base damage; halving it gives 900 damage.

Replanted and Fusion have different zombie and leveling systems. This mod carries over difficulty rules, **no Fusion levels or Fusion-exclusive zombie tables**. Fixed enemies in special levels are not replaced by other zombie types. The visible balance must be checked in the game.

## Scores and profiles

The mod leaves the native storage format unchanged. `UserData/DifficultyMod/progress.json` contains the highest level gained as well as the difficulty of saved levels, separated by profile and level. SHA256 of the actual saved native buffer prevents stale metadata from being mapped to another save. Changes are replaced via a temporary file; the previous file remains as `.bak`. A corrupted progress file will not be overwritten; the error appears in the MelonLoader log.

A saved level will only be continued if the difficulty is the same. If the difficulty differs, the mod will refuse to load. The native starting path restarts the level, including plant selection if that level has a plant selection. Existing or externally modified savegames without appropriate mod metadata are treated as **Normal**. Other stages restart these levels.

The mod only writes difficulty metadata when the native save process actually creates a modified save buffer. Returning early when leaving the plant selection may not add a new difficulty to an older save game.

## Build and install

```bash
rtk proxy python3 /home/kevin/modding/pvzreplanted/difficulty-mod/tools/install.py
```

The tool compiles against the existing IL2CPP interop assemblies, atomically installs `Mods/DifficultyMod.dll` and checks its SHA256. An existing different mod DLL is previously saved in the source folder. An existing configuration is retained. The tool does not start the game or tests.

Supported `GameAssembly.dll`: `e7333eaf87996fa44b4a3526986272319b7d01f7acdbcff52451652f61ceb696`. After a game update, the native processes must be checked again. The static documents are under `research/`.

To remove with game closed, remove `Mods/DifficultyMod.dll`. Progress file and configuration can be retained for later reinstallation. A level saved under a higher difficulty should be restarted after removing the mod, as its already saved waves may be retained.

## Check status and manual check

Version 1.0.4 has been compiled. **The agent did not start the game and did not run any tests.** The screenshot `Bildschirmfoto_20261103_081202.png`, the Plant Library implementation and the generated icon were checked visually and statically. The display in the game remains to be checked by the user. The five extreme hits recorded continue to support manual damage assessment.

For your own examination:

1. Click on the adventure card and one mini-game, puzzle and survival card with the left or right. On the right, all five previews must appear before plant selection. Cancel is not allowed to start a level.
2. Right-click on a blocked card. A locked level is not allowed to start.
3. Complete a higher victory and then a lower victory. The cup must retain the higher rank; Changing profiles and restarting the game must show separate progress.
4. Win a level on a different level and have the next level start automatically. The difficulty must be maintained. Then return to the menu and select again using a left click; The set standard must now apply.
5. Save a current game and continue at the same level. Then select the same card at a different level; the level must restart. Also check changing the standard in the options.
6. Leave another stage's plant selection without playing. An older save game must not receive incorrect difficulty metadata.
7. Check Easy Damage, Wave Differences, Harder Burn and Extreme Hypnosis, Damage and Speed. Judge special mini-games with given waves separately.
