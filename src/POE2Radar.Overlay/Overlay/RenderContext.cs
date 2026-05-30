using POE2Radar.Core.Game;
using NumVec2 = System.Numerics.Vector2;

namespace POE2Radar.Overlay;

/// <summary>What the PoE2 renderer needs each frame. Built fresh by <see cref="RadarApp"/>.</summary>
public sealed record RenderContext(
    bool InGame,
    bool Active,            // PoE2 is the foreground window — draw nothing when false
    int WindowWidth,
    int WindowHeight,
    NumVec2 PlayerGrid,
    Poe2Live.MapUi Map,
    IReadOnlyList<Poe2Live.EntityDot> Entities,
    IReadOnlyList<Poe2Live.Landmark> Landmarks,
    uint AreaHash,
    Poe2Live.TerrainData? Terrain,
    // Live projection calibration (adjustable at runtime).
    float ScaleMul,
    float OffsetX,
    float OffsetY,
    // Auto-flask status.
    float HpPct,
    float ManaPct,
    string FlaskNote,
    // Area / character HUD.
    string AreaCode,
    int CharLevel,
    // WorldToScreen matrix (16 floats, row-major) for world-space nameplates; null if unavailable.
    float[]? CameraMatrix);
