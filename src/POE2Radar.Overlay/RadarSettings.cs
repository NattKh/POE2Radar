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
    public bool PersistEntities { get; set; } = true;

    // Per-category label toggles (dot still shown; only the text label is hidden)
    public bool ShowMonsterLabels { get; set; } = false;
    public bool ShowChestLabels { get; set; } = false;
    public bool ShowTransitionLabels { get; set; } = true;
    public bool ShowNpcLabels { get; set; } = true;
    public bool ShowLandmarkLabels { get; set; } = true;
    public bool ShowPoiLabels { get; set; } = true;

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

    // Fonts (no cap — scale up for 4K)
    public string FontFamily { get; set; } = "Consolas";
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

    // Minimap
    public bool ShowMinimap { get; set; } = false;
    public float MinimapSize { get; set; } = 250f;
    public float MinimapScale { get; set; } = 0.5f;
    public float MinimapOpacity { get; set; } = 0.85f;
    public string MinimapPosition { get; set; } = "bottomright";
    // Terrain edge (the map outline)
    public string TerrainEdgeColor { get; set; } = "#3cdcff";
    public float TerrainEdgeAlpha { get; set; } = 0.7f;
    public float TerrainInteriorAlpha { get; set; } = 0.12f;

    public float MinimapOffsetX { get; set; } = 0f;
    public float MinimapOffsetY { get; set; } = 0f;
    public float MinimapLabelFontSize { get; set; } = 9f;

    // Minimap — what to show (dots)
    public bool MinimapShowMonsters { get; set; } = true;
    public bool MinimapShowBosses { get; set; } = true;
    public bool MinimapShowNpcs { get; set; } = true;
    public bool MinimapShowChests { get; set; } = true;
    public bool MinimapShowTransitions { get; set; } = true;
    public bool MinimapShowTerrain { get; set; } = true;
    public bool MinimapShowPath { get; set; } = true;

    // Minimap — what labels to display
    public bool MinimapLabelBoss { get; set; } = true;
    public bool MinimapLabelUnique { get; set; } = true;
    public bool MinimapLabelTransition { get; set; } = true;
    public bool MinimapLabelNpc { get; set; } = true;
    public bool MinimapLabelWatched { get; set; } = true;

    // Minimap — dot sizes
    public float MinimapDotScale { get; set; } = 1f;

    // Zone Guide
    public float ZoneGuideTitleFontSize { get; set; } = 22f;
    public float ZoneGuideBodyFontSize { get; set; } = 16f;

    // Exploration fog
    public bool ShowExplorationFog { get; set; } = true;
    public float FogOpacity { get; set; } = 0.45f;

    // Junk filter
    public bool HideJunkEntities { get; set; } = true;

    // Boss / Targetable
    public bool ShowBossHighlight { get; set; } = true;
    public float BossDotSize { get; set; } = 8.0f;
    public bool HideUntargetable { get; set; } = false;

    // Nameplate HP bars
    public float NameplateBarWidth { get; set; } = 1.0f;
    public float NameplateBarHeight { get; set; } = 5f;
    public float NameplateOffsetY { get; set; } = -30f;

    // Pathfinding
    public bool ShowPath { get; set; } = true;
    public bool ShowGroundWaypoints { get; set; } = true;
    public int PathMaxNodes { get; set; } = 2_000_000;
    public string PathTarget { get; set; } = "";
    public string PathColor { get; set; } = "#00ffcc";
    public float PathWidth { get; set; } = 2.5f;

    // Terrain
    public float TerrainOpacity { get; set; } = 1.0f;

    // Performance
    public int FpsCap { get; set; } = 60;

    // Calibration
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public float ScaleMul { get; set; } = 1.0f;
    public bool ResetCalibrationOnZoneChange { get; set; } = true;

    // Flask
    public float HpThreshold { get; set; } = 65f;
    public float ManaThreshold { get; set; } = 30f;
    public int FlaskLifeKey { get; set; } = 0x31;
    public int FlaskManaKey { get; set; } = 0x32;
    public int FlaskLifeCooldownMs { get; set; } = 2500;
    public int FlaskManaCooldownMs { get; set; } = 2000;

    // Hotkey bindings (Windows VK codes)
    public int KeyCheat1 { get; set; } = 0x70;           // F1
    public int KeyCheat2 { get; set; } = 0x71;           // F2
    public int KeyCheat3 { get; set; } = 0x72;           // F3
    public int KeyCheat4 { get; set; } = 0x73;           // F4
    public int KeyCheat5 { get; set; } = 0x74;           // F5
    public int KeyCycleLandmarks { get; set; } = 0x75;   // F6
    public int KeyCycleEntities { get; set; } = 0x76;    // F7
    public int KeyAutoFlask { get; set; } = 0x77;        // F8
    public int KeySettings { get; set; } = 0x78;         // F9
    public int KeyToggleOverlay { get; set; } = 0x79;    // F10
    public int KeyDashboard { get; set; } = 0x7A;        // F11

    // Visual Clutter Reduction
    public bool ShowNormalMonsters { get; set; } = true;
    public bool ShowNormalChests { get; set; } = false;
    public bool ShowDeadMonsters { get; set; } = false;
    public bool ShowFriendlyEntities { get; set; } = true;
    public bool ShowImmobileEntities { get; set; } = true;
    public float EntityDrawRange { get; set; } = 0f;
    public float MinEntityHpPct { get; set; } = 0f;
    public bool ShowDistanceRing { get; set; } = false;
    public float DistanceRingRadius { get; set; } = 80f;

    // Game Visual Tweaks — Render (writes to game memory)
    public bool TweakHideNormalLifeBars { get; set; } = false;
    public bool TweakHideMagicLifeBars { get; set; } = false;
    public bool TweakHideAllLifeBars { get; set; } = false;
    public bool TweakHideBuffVisuals { get; set; } = false;
    public bool TweakHideNormalRendering { get; set; } = false;
    public bool TweakForceShowHover { get; set; } = false;
    public bool TweakDisableSelectionBoxes { get; set; } = false;
    public bool TweakHideInfoDisplay { get; set; } = false;
    public bool TweakHideTalismanIcons { get; set; } = false;
    public bool TweakForceOutline { get; set; } = false;

    // Game Tweaks — Physics (Positioned)
    public bool TweakDisableMonsterBlocking { get; set; } = false;
    public bool TweakDisableMonsterPush { get; set; } = false;
    public bool TweakEnablePhaseThrough { get; set; } = false;

    // Game Tweaks — Targeting
    public bool TweakForceAllTargetable { get; set; } = false;
    public bool TweakForceAllAttackable { get; set; } = false;

    // Game Tweaks — Behavior
    public bool TweakFreezeNormalMonsters { get; set; } = false;
    public bool TweakPreventCorpseSinking { get; set; } = false;

    // Game Tweaks — Impactful
    public float TweakEntityColorR { get; set; } = -1f;
    public float TweakEntityColorG { get; set; } = -1f;
    public float TweakEntityColorB { get; set; } = -1f;
    public float TweakEntityScale { get; set; } = 0f;
    public bool TweakSwapTeamToFriendly { get; set; } = false;
    public bool TweakInstantTransitions { get; set; } = false;
    public bool TweakUnblockDoors { get; set; } = false;
    public bool TweakUnlockChests { get; set; } = false;
    public bool TweakOpenChestsOnDamage { get; set; } = false;
    public bool TweakMakeAllBoss { get; set; } = false;
    public bool TweakRemoveBossFlag { get; set; } = false;
    public float TweakLabelViewDistance { get; set; } = 0f;

    // Game Tweaks — DevTest
    public bool TweakDevHideHover { get; set; } = false;
    public bool TweakDevFadeArrows { get; set; } = false;
    public bool TweakDevDisableLight { get; set; } = false;
    public bool TweakDevFixedSelectionSize { get; set; } = false;
    public bool TweakDevBBoxIgnoreGround { get; set; } = false;
    public bool TweakDevFaceWindDirection { get; set; } = false;
    public bool TweakDevDampenHeight { get; set; } = false;
    public float TweakDevHeightOffset { get; set; } = 0f;
    public float TweakDevSelectionHeightOverride { get; set; } = 0f;
    public bool TweakDevLockOrientation { get; set; } = false;
    public bool TweakDevMakeFlying { get; set; } = false;
    public bool TweakDevMakeStatic { get; set; } = false;
    public bool TweakDevFaceMovementDir { get; set; } = false;
    public bool TweakDevAvoidOthers { get; set; } = false;
    public bool TweakDevLockAnimation { get; set; } = false;
    public bool TweakDevCorpseUsable { get; set; } = false;
    public bool TweakDevNoCorpseMarker { get; set; } = false;

    // Game Tweaks — Scope
    public bool TweakApplyToNpcs { get; set; } = false;
    public bool TweakApplyToChests { get; set; } = false;

    // Auto-Logout (force-kill game on low HP to prevent death/XP loss)
    public bool AutoLogoutEnabled { get; set; } = false;
    public float AutoLogoutHpThreshold { get; set; } = 35f;

    // Per-item icon styling (shape / color / opacity / size) + metadata-matched mechanic overrides
    public RadarStyles Styles { get; set; } = new();

    // Monster HP-bar geometry
    public HpBarSettings HpBars { get; set; } = new();

    // Walkable-terrain bitmap colors/transparency
    public TerrainStyle Terrain { get; set; } = new();

    // Map drawing
    public float MapCenterYShift { get; set; } = -20f;
    public float PlayerBlipSize { get; set; } = 5f;
    public float MinimapPlayerBlipSize { get; set; } = 4f;
    public float LandmarkIconSize { get; set; } = 5f;
    public float PathEndMarkerSize { get; set; } = 5f;
    public int FogGridStep { get; set; } = 4;
    public float FogCellScale { get; set; } = 0.12f;
    public float ClickInspectDistance { get; set; } = 40f;

    // Persistence
    private string? _filePath;

    public void SetPath(string path) => _filePath = path;

    public void ResetToDefaults()
    {
        var defaults = new RadarSettings();
        foreach (var prop in typeof(RadarSettings).GetProperties())
        {
            if (!prop.CanWrite || prop.Name == "FilePath") continue;
            try { prop.SetValue(this, prop.GetValue(defaults)); } catch { }
        }
        Save();
    }

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

public sealed class IconStyle
{
    public bool Enabled { get; set; } = true;
    public string Shape { get; set; } = "Circle";
    public string Color { get; set; } = "#FFFFFF";
    public float Opacity { get; set; } = 1.0f;
    public float Size { get; set; } = 3.0f;

    public IconStyle() { }
    public IconStyle(string shape, string color, float opacity, float size)
    {
        Shape = shape; Color = color; Opacity = opacity; Size = size;
    }
}

public sealed class MechanicStyle
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "";
    public List<string> Match { get; set; } = new();
    public string Shape { get; set; } = "Star";
    public string Color { get; set; } = "#FFFFFF";
    public float Opacity { get; set; } = 1.0f;
    public float Size { get; set; } = 6.0f;
}

public sealed class HpBarSettings
{
    public float Height { get; set; } = 5f;
    public float OffsetX { get; set; } = 0f;
    public float OffsetY { get; set; } = -30f;
    public float WidthNormal { get; set; } = 30f;
    public float WidthMagic { get; set; } = 38f;
    public float WidthRare { get; set; } = 50f;
    public float WidthUnique { get; set; } = 64f;
}

public sealed class TerrainStyle
{
    public string InteriorColor { get; set; } = "#506482";
    public float InteriorOpacity { get; set; } = 0.118f;
    public string EdgeColor { get; set; } = "#3CDCFF";
    public float EdgeOpacity { get; set; } = 0.706f;
}

public sealed class RadarStyles
{
    public IconStyle MonsterNormal { get; set; } = new("Circle",   "#FF3333", 0.95f, 2.6f);
    public IconStyle MonsterMagic  { get; set; } = new("Diamond",  "#73A6FF", 0.97f, 3.4f);
    public IconStyle MonsterRare   { get; set; } = new("Triangle", "#FFD926", 1.00f, 5.5f);
    public IconStyle MonsterUnique { get; set; } = new("Star",     "#FF7300", 1.00f, 6.5f);

    public IconStyle Player        { get; set; } = new("Circle",  "#4DF2FF", 1.00f, 3.0f);
    public IconStyle Npc           { get; set; } = new("Plus",    "#FFD933", 0.95f, 4.0f);
    public IconStyle ChestRare     { get; set; } = new("Square",  "#FFD926", 0.95f, 5.0f);
    public IconStyle ChestUnique   { get; set; } = new("Square",  "#FF7300", 0.95f, 5.0f);
    public IconStyle Transition    { get; set; } = new("Diamond", "#66FF99", 0.95f, 4.5f);
    public IconStyle Poi           { get; set; } = new("Circle",  "#8CBFFF", 0.70f, 3.0f);
    public IconStyle Landmark      { get; set; } = new("Diamond", "#F259F2", 1.00f, 5.0f);

    public List<MechanicStyle> Mechanics { get; set; } = new()
    {
        new() { Name = "Expedition", Match = ["ExpeditionEncounter", "Expedition"], Shape = "Plus",     Color = "#26E6D9", Opacity = 1f, Size = 7f },
        new() { Name = "Ritual",     Match = ["Ritual"],                            Shape = "Star",     Color = "#FF3355", Opacity = 1f, Size = 7f },
        new() { Name = "Breach",     Match = ["Breach"],                            Shape = "Diamond",  Color = "#A64DFF", Opacity = 1f, Size = 7f },
        new() { Name = "Strongbox",  Match = ["Strongbox", "StrongBoxes"],          Shape = "Square",   Color = "#FFB300", Opacity = 1f, Size = 6f },
        new() { Name = "Essence",    Match = ["Essence"],                           Shape = "Triangle", Color = "#33E0FF", Opacity = 1f, Size = 7f },
        new() { Name = "Shrine",     Match = ["Shrine"],                            Shape = "Star",     Color = "#7DFF7D", Opacity = 1f, Size = 6f },
    };
}
