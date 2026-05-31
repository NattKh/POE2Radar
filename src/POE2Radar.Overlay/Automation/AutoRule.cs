using System.Text.Json;

namespace POE2Radar.Overlay.Automation;

public sealed class AutoRule
{
    public string Name { get; set; } = "";
    public int Key { get; set; }
    public bool Enabled { get; set; } = true;
    public float CooldownSec { get; set; } = 1.0f;

    // Conditions (all must be true)
    public float? HpBelow { get; set; }
    public float? HpAbove { get; set; }
    public float? ManaBelow { get; set; }
    public float? ManaAbove { get; set; }
    public int? EnemiesNearby { get; set; }
    public int? EnemiesNearbyRadius { get; set; }

    // Runtime
    public DateTime LastFired { get; set; } = DateTime.MinValue;
}

public sealed class AutoRuleEngine
{
    private readonly string _filePath;
    private List<AutoRule> _rules = new();
    private bool _enabled = true;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public bool Enabled { get => _enabled; set => _enabled = value; }
    public IReadOnlyList<AutoRule> Rules => _rules;

    public AutoRuleEngine(string filePath)
    {
        _filePath = filePath;
        Load();
        if (_rules.Count == 0) LoadDefaults();
    }

    public bool Evaluate(AutoRule rule, float hpPct, float manaPct, int nearbyEnemies)
    {
        if (!_enabled || !rule.Enabled) return false;

        var now = DateTime.UtcNow;
        if ((now - rule.LastFired).TotalSeconds < rule.CooldownSec) return false;

        if (rule.HpBelow.HasValue && hpPct >= rule.HpBelow.Value) return false;
        if (rule.HpAbove.HasValue && hpPct <= rule.HpAbove.Value) return false;
        if (rule.ManaBelow.HasValue && manaPct >= rule.ManaBelow.Value) return false;
        if (rule.ManaAbove.HasValue && manaPct <= rule.ManaAbove.Value) return false;
        if (rule.EnemiesNearby.HasValue && nearbyEnemies < rule.EnemiesNearby.Value) return false;

        return true;
    }

    public void MarkFired(AutoRule rule) => rule.LastFired = DateTime.UtcNow;

    public void Add(AutoRule rule) { _rules.Add(rule); Save(); }

    public void Remove(int index)
    {
        if (index >= 0 && index < _rules.Count) { _rules.RemoveAt(index); Save(); }
    }

    public void Update(int index, AutoRule rule)
    {
        if (index >= 0 && index < _rules.Count) { _rules[index] = rule; Save(); }
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(_rules, Json));
        }
        catch { }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            _rules = JsonSerializer.Deserialize<List<AutoRule>>(File.ReadAllText(_filePath), Json) ?? new();
        }
        catch { _rules = new(); }
    }

    private void LoadDefaults()
    {
        _rules =
        [
            new() { Name = "Life Flask", Key = 0x31, HpBelow = 50f, CooldownSec = 2.5f },
            new() { Name = "Mana Flask", Key = 0x32, ManaBelow = 30f, CooldownSec = 2.0f },
            new() { Name = "Guard Skill", Key = 0x52, HpBelow = 60f, EnemiesNearby = 3, CooldownSec = 4.0f, Enabled = false },
        ];
        Save();
        Console.WriteLine($"  Loaded {_rules.Count} default auto-skill rules");
    }
}
