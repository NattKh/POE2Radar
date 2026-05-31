using System.Text.Json;

namespace POE2Radar.Overlay.Web;

public sealed class WatchedEntities
{
    private readonly string _filePath;
    private readonly Dictionary<string, WatchedEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public WatchedEntities(string filePath)
    {
        _filePath = filePath;
        Load();
    }

    public IReadOnlyDictionary<string, WatchedEntry> All => _entries;

    public bool IsWatched(string metadata)
    {
        foreach (var (pattern, _) in _entries)
            if (metadata.Contains(pattern, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public WatchedEntry? Match(string metadata)
    {
        foreach (var (pattern, entry) in _entries)
            if (metadata.Contains(pattern, StringComparison.OrdinalIgnoreCase)) return entry;
        return null;
    }

    public void Add(string pattern, string label, string color)
    {
        _entries[pattern] = new WatchedEntry(pattern, label, color, true);
        Save();
    }

    public void Remove(string pattern)
    {
        _entries.Remove(pattern);
        Save();
    }

    public void Toggle(string pattern, bool enabled)
    {
        if (_entries.TryGetValue(pattern, out var e))
        {
            _entries[pattern] = e with { Enabled = enabled };
            Save();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            var list = JsonSerializer.Deserialize<List<WatchedEntry>>(json, Json);
            if (list == null) return;
            foreach (var e in list) _entries[e.Pattern] = e;
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(_entries.Values.ToList(), Json));
        }
        catch (Exception ex) { Console.WriteLine($"Failed to save watched entities: {ex.Message}"); }
    }
}

public sealed record WatchedEntry(string Pattern, string Label, string Color, bool Enabled);
