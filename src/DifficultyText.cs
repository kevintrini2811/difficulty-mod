namespace DifficultyMod;

internal static class DifficultyText
{
    internal static string Name(Difficulty difficulty, bool german) => (difficulty, german) switch
    {
        (Difficulty.Easy, true) => "Leicht",
        (Difficulty.Medium, true) => "Mittel",
        (Difficulty.Normal, true) => "Normal",
        (Difficulty.Harder, true) => "Schwerer",
        (Difficulty.Extreme, true) => "Extrem",
        _ => difficulty.ToString()
    };

    internal static string Title(bool settings, bool german) => (settings, german) switch
    {
        (true, true) => "Standardschwierigkeit",
        (true, false) => "Default difficulty",
        (false, true) => "Schwierigkeit wählen",
        _ => "Choose difficulty"
    };
    internal static string Settings(bool german) => german ? "Schwierigkeit" : "Difficulty";
    internal static string Cancel(bool german) => german ? "Abbrechen" : "Cancel";
}
