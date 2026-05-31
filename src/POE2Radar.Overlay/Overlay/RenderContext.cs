using POE2Radar.Core.Cheats;
using POE2Radar.Core.Game;
using NumVec2 = System.Numerics.Vector2;

namespace POE2Radar.Overlay;

public sealed record RenderContext(
    bool InGame,
    bool Active,
    int WindowWidth,
    int WindowHeight,
    NumVec2 PlayerGrid,
    Poe2Live.MapUi Map,
    IReadOnlyList<Poe2Live.EntityDot> Entities,
    IReadOnlyList<Poe2Live.Landmark> Landmarks,
    uint AreaHash,
    Poe2Live.TerrainData? Terrain,
    float ScaleMul,
    float OffsetX,
    float OffsetY,
    float HpPct,
    float ManaPct,
    string FlaskNote,
    string AreaCode,
    int CharLevel,
    float[]? CameraMatrix,
    IReadOnlyDictionary<string, CheatInfo>? CheatStatus = null,
    RadarSettings? Radar = null,
    bool OverlayVisible = true,
    POE2Radar.Overlay.Web.WatchedEntities? Watched = null,
    List<(int X, int Y)>? PathPoints = null,
    List<(float ScreenX, float ScreenY, string Metadata)>? EntityScreenPositions = null);
