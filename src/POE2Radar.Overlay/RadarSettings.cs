using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace POE2Radar.Overlay;

public sealed class RadarSettings
{
    // Visibility
    public bool ShowMonsters { get; set; } = true;
    public bool ShowRareMonsters { get; set; } = true;
    public bool ShowUniqueMonsters { get; set; } = true;
    public bool ShowNpcs { get; set; } = true;
    public bool ShowChests { get; set; } = true;
    public bool ShowTransitions { get; set; } = true;
    public bool ShowPlayers { get; set; } = true;
    public bool ShowLandmarks { get; set; } = true;
    public bool ShowNameplates { get; set; } = true;
    public bool ShowTerrain { get; set; } = true;
    public bool ShowStatusBar { get; set; } = true;
    public bool ShowWatchedLabels { get; set; } = true;

    // Dot sizes (no cap)
    public float MonsterDotSize { get; set; } = 2.6f;
    public float MagicDotSize { get; set; } = 3.4f;
    public float RareDotSize { get; set; } = 5.5f;
    public float UniqueDotSize { get; set; } = 6.5f;
    public float NpcDotSize { get; set; } = 4.0f;
    public float ChestDotSize { get; set; } = 5.0f;
    public float TransitionDotSize { get; set; } = 4.5f;
    public float PlayerDotSize { get; set; } = 5.0f;
    public float WatchedDotSize { get; set; } = 7.0f;

    // Outline
    public float DotOutlineWidth { get; set; } = 0f;
    public string DotOutlineColor { get; set; } = "#ffffff";
    public float LandmarkOutlineWidth { get; set; } = 1.6f;

    // Font sizes (no cap — scale up for 4K)
    public float StatusFontSize { get; set; } = 12f;
    public float LandmarkFontSize { get; set; } = 14f;
    public float TransitionFontSize { get; set; } = 12f;
    public float ChestFontSize { get; set; } = 12f;
    public float WatchedFontSize { get; set; } = 14f;
    public float NameplateFontSize { get; set; } = 12f;

    // Colors (hex strings for easy JSON/web serialization)
    public string MonsterColor { get; set; } = "#ff3333";
    public string MagicColor { get; set; } = "#73a6ff";
    public string RareColor { get; set; } = "#ffd926";
    public string UniqueColor { get; set; } = "#ff7300";
    public string NpcColor { get; set; } = "#ffd933";
    public string ChestColor { get; set; } = "#f28c1a";
    public string TransitionColor { get; set; } = "#66ff99";
    public string PlayerColor { get; set; } = "#4df2ff";
    public string LandmarkColor { get; set; } = "#f259f2";
    public string WatchedColor { get; set; } = "#ffffff";
    public string TerrainColor { get; set; } = "#1a3a1a";

    // Junk filter
    public bool HideJunkEntities { get; set; } = true;

    // Pathfinding
    public bool ShowPath { get; set; } = true;
    public string PathTarget { get; set; } = "";
    public string PathColor { get; set; } = "#00ffcc";
    public float PathWidth { get; set; } = 2.5f;

    // Terrain
    public float TerrainOpacity { get; set; } = 1.0f;

    // Calibration
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public float ScaleMul { get; set; } = 1.0f;

    // Flask
    public float HpThreshold { get; set; } = 65f;
    public float ManaThreshold { get; set; } = 30f;

    // Persistence
    private string? _filePath;

    public void SetPath(string path) => _filePath = path;

    public void Save()
    {
        if (_filePath == null) return;
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { }
    }

    public static RadarSettings Load(string path)
    {
        RadarSettings s;
        try
        {
            if (File.Exists(path))
                s = JsonSerializer.Deserialize<RadarSettings>(File.ReadAllText(path), JsonOpts) ?? new();
            else
                s = new();
        }
        catch { s = new(); }
        s._filePath = path;
        return s;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
