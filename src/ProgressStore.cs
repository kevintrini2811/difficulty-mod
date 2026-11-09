using System.Text.Json;

namespace DifficultyMod;

internal sealed class ProgressData
{
    public int Version { get; set; } = 1;
    public Dictionary<string, int> Best { get; set; } = new();
    public Dictionary<string, SavedDifficulty> Saves { get; set; } = new();
}

internal sealed class SavedDifficulty
{
    public int Difficulty { get; set; }
    public string Fingerprint { get; set; } = "";
}

// This file is separate from native saves. Failed reads never become an empty
// overwrite. A failed write preserves the last committed file and logs an error.
internal sealed class ProgressStore
{
    private readonly string path;
    private readonly Action<string> error;
    private ProgressData data = new();
    private bool writable = true;

    internal ProgressStore(string path, Action<string> error)
    {
        this.path = path; this.error = error;
        if (!File.Exists(path)) return;
        try
        {
            data = JsonSerializer.Deserialize<ProgressData>(File.ReadAllText(path))
                ?? throw new InvalidDataException("Empty difficulty progress file.");
            if (data.Version != 1 || data.Best == null || data.Saves == null)
                throw new InvalidDataException("Unsupported difficulty progress file.");
            if (data.Best.Any(item => !DifficultyRules.Valid(item.Value)) ||
                data.Saves.Any(item => item.Value == null || !DifficultyRules.Valid(item.Value.Difficulty)))
                throw new InvalidDataException("Invalid difficulty value in progress file.");
        }
        catch (Exception exception)
        {
            writable = false; data = new();
            error($"Difficulty progress cannot be read; existing file is preserved: {exception.Message}");
        }
    }

    internal int Best(string key) => data.Best.GetValueOrDefault(key);
    internal Difficulty? Saved(string key, string fingerprint) =>
        data.Saves.TryGetValue(key, out var save) && save.Fingerprint == fingerprint
            ? DifficultyRules.Validate(save.Difficulty) : null;
    internal void Win(string key, Difficulty difficulty)
    {
        if (Best(key) >= (int)difficulty) return;
        data.Best[key] = (int)difficulty; Write();
    }
    internal void Save(string key, Difficulty difficulty, string fingerprint)
    {
        data.Saves[key] = new() { Difficulty = (int)difficulty, Fingerprint = fingerprint }; Write();
    }
    private void Write()
    {
        if (!writable) return;
        string temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, data, new JsonSerializerOptions { WriteIndented = true });
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.Move(temporary, path, true);
        }
        catch (Exception exception) { error($"Difficulty progress write failed: {exception.Message}"); }
    }
}
