using System.Numerics;
using POE2Radar.Core.Game;
using POE2Radar.Core.Pathfinding;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using NumVec2 = System.Numerics.Vector2;
using GameVec2 = POE2Radar.Core.Game.Vector2;

namespace POE2Radar.Overlay;

/// <summary>
/// PoE2 radar overlay. When the large map is open, draws the walkable-terrain bitmap, entity
/// dots (enemies, NPCs, etc.), and the player blip, projected player-centered onto the map with
/// the same isometric math the PoE Radar plugin uses. Projection scale/offset are calibratable
/// at runtime (see <see cref="RadarApp"/>).
/// </summary>
public sealed class OverlayRenderer : IDisposable
{
    private static readonly Color4 ColPlayer  = new(0.30f, 0.95f, 1.00f, 1.00f);
    private static readonly Color4 ColMonster = new(1.00f, 0.20f, 0.20f, 0.95f); // Normal
    private static readonly Color4 ColMagic   = new(0.45f, 0.65f, 1.00f, 0.97f); // Magic (blue)
    private static readonly Color4 ColRare    = new(1.00f, 0.85f, 0.15f, 1.00f); // Rare (gold)
    private static readonly Color4 ColUnique  = new(1.00f, 0.45f, 0.00f, 1.00f); // Unique (orange)
    private static readonly Color4 ColNpc     = new(1.00f, 0.85f, 0.20f, 0.95f);
    private static readonly Color4 ColChest   = new(0.95f, 0.55f, 0.10f, 0.95f);
    private static readonly Color4 ColTrans   = new(0.40f, 1.00f, 0.60f, 0.95f);
    private static readonly Color4 ColObject  = new(0.55f, 0.75f, 1.00f, 0.70f);
    private static readonly Color4 ColOther   = new(0.70f, 0.70f, 0.70f, 0.60f);
    private static readonly Color4 ColText    = new(1f, 1f, 1f, 1f);
    private static readonly Color4 ColPanel   = new(0.05f, 0.05f, 0.05f, 0.78f);
    private static readonly Color4 ColLandmark = new(0.95f, 0.35f, 0.95f, 1f); // magenta — static tile landmarks

    private readonly OverlayWindow _window;
    private TerrainBitmap? _terrain;

    private ID2D1SolidColorBrush? _bPlayer, _bMonster, _bNpc, _bChest, _bTrans, _bObject, _bOther, _bText, _bPanel, _bLandmark;
    private ID2D1SolidColorBrush? _bMagic, _bRare, _bUnique;
    private IDWriteTextFormat? _tf;
    private bool _ready;

    public OverlayRenderer(OverlayWindow window) { _window = window; }

    private void EnsureResources()
    {
        if (_ready) return;
        var rt = _window.RenderTarget;
        _bPlayer  = rt.CreateSolidColorBrush(ColPlayer);
        _bMonster = rt.CreateSolidColorBrush(ColMonster);
        _bNpc     = rt.CreateSolidColorBrush(ColNpc);
        _bChest   = rt.CreateSolidColorBrush(ColChest);
        _bTrans   = rt.CreateSolidColorBrush(ColTrans);
        _bObject  = rt.CreateSolidColorBrush(ColObject);
        _bOther   = rt.CreateSolidColorBrush(ColOther);
        _bText    = rt.CreateSolidColorBrush(ColText);
        _bPanel   = rt.CreateSolidColorBrush(ColPanel);
        _bLandmark = rt.CreateSolidColorBrush(ColLandmark);
        _bMagic   = rt.CreateSolidColorBrush(ColMagic);
        _bRare    = rt.CreateSolidColorBrush(ColRare);
        _bUnique  = rt.CreateSolidColorBrush(ColUnique);
        _tf = _window.DWriteFactory.CreateTextFormat("Consolas", null, FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 12f, "en-us");
        _ready = true;
    }

    public void Render(RenderContext ctx)
    {
        if (!_window.IsValid) return;
        EnsureResources();
        var rt = _window.RenderTarget;
        rt.BeginDraw();
        rt.Clear(new Color4(0f, 0f, 0f, 0f));
        rt.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Grayscale;
        try
        {
            // Draw nothing unless PoE2 is the foreground window — so the overlay never shows
            // over other apps when you alt-tab. (The cleared frame above hides prior content.)
            if (ctx.Active)
            {
                DrawStatus(rt, ctx);
                if (ctx.InGame) DrawNameplates(rt, ctx);   // world-space HP bars over elite mobs
                if (ctx is { InGame: true, Map.IsVisible: true })
                    DrawMap(rt, ctx);
            }
        }
        finally { rt.EndDraw(); }
        _window.Present();
    }

    private void DrawStatus(ID2D1RenderTarget rt, RenderContext ctx)
    {
        int enemies = 0, uniques = 0, rares = 0;
        foreach (var e in ctx.Entities)
            if (e.Category == Poe2Live.EntityCategory.Monster && e.IsAlive)
            {
                enemies++;
                if (e.Rarity == Poe2Live.Rarity.Unique) uniques++;
                else if (e.Rarity == Poe2Live.Rarity.Rare) rares++;
            }

        var line = !ctx.InGame
            ? "POE2Radar: waiting for in-game…"
            : $"POE2Radar  {ctx.AreaCode} (lvl {ctx.CharLevel})  HP {ctx.HpPct:F0}%  MP {ctx.ManaPct:F0}%  "
              + $"flask:{ctx.FlaskNote}  enemies:{enemies} (R{rares} U{uniques})"
              + (ctx.Map.IsVisible
                  ? $"  | map zoom={ctx.Map.Zoom:F2} scale={ctx.ScaleMul:F2} off=({ctx.OffsetX:F0},{ctx.OffsetY:F0})"
                  : "");
        rt.FillRectangle(new Vortice.RawRectF(6, 6, 6 + line.Length * 7.3f + 10, 26), _bPanel!);
        rt.DrawText(line, _tf!, new Rect(12, 8, 1200, 22), _bText!, DrawTextOptions.Clip);
    }

    /// <summary>
    /// World-space HP bars over Magic/Rare/Unique monsters, projected via the camera WorldToScreen
    /// matrix. Drawn whether or not the big map is open (it's a heads-up combat overlay).
    /// </summary>
    private void DrawNameplates(ID2D1RenderTarget rt, RenderContext ctx)
    {
        if (ctx.CameraMatrix is not { } m) return;
        float W = ctx.WindowWidth, H = ctx.WindowHeight;
        foreach (var e in ctx.Entities)
        {
            if (e.Category != Poe2Live.EntityCategory.Monster || !e.IsAlive || e.HpMax <= 0) continue;
            if (e.Rarity is Poe2Live.Rarity.Normal or Poe2Live.Rarity.NonMonster) continue; // Magic/Rare/Unique only

            var w = e.World;
            var cw = w.X*m[3] + w.Y*m[7] + w.Z*m[11] + m[15];
            if (cw <= 0.0001f) continue;
            var cx = w.X*m[0] + w.Y*m[4] + w.Z*m[8] + m[12];
            var cy = w.X*m[1] + w.Y*m[5] + w.Z*m[9] + m[13];
            var sx = (cx/cw/2f + 0.5f) * W;
            var sy = (0.5f - cy/cw/2f) * H;
            if (sx < 0 || sx > W || sy < 0 || sy > H) continue;

            var (col, bw) = e.Rarity switch
            {
                Poe2Live.Rarity.Unique => (_bUnique!, 64f),
                Poe2Live.Rarity.Rare   => (_bRare!, 50f),
                _                      => (_bMagic!, 38f),
            };
            const float bh = 5f;
            var bx = sx - bw / 2f;
            var by = sy - 30f; // sit above the mob
            var frac = e.HpFraction;
            rt.FillRectangle(new Vortice.RawRectF(bx, by, bx + bw, by + bh), _bPanel!);
            var fill = frac < 0.3f ? _bMonster! : col;
            rt.FillRectangle(new Vortice.RawRectF(bx, by, bx + bw * frac, by + bh), fill);
            rt.DrawRectangle(new Vortice.RawRectF(bx, by, bx + bw, by + bh), col, 1f);
        }
    }

    private void DrawMap(ID2D1RenderTarget rt, RenderContext ctx)
    {
        // MapCenter = window center + DefaultShift(0,-20) + Shift + manual offset.
        var center = new NumVec2(
            ctx.WindowWidth  * 0.5f + ctx.Map.ShiftX + ctx.OffsetX,
            ctx.WindowHeight * 0.5f + ctx.Map.ShiftY - 20f + ctx.OffsetY);
        var scale = ctx.Map.Zoom * (ctx.WindowHeight / 677f) * ctx.ScaleMul;
        var player = ctx.PlayerGrid;

        // Terrain bitmap, projected via the same affine grid→screen transform.
        if (ctx.Terrain is { } t)
        {
            _terrain ??= new TerrainBitmap(rt);
            _terrain.EnsureBuiltRaw(t.Walkable, t.Width, t.Height, ctx.AreaHash, inTransition: false);
            if (_terrain.Bitmap is { } bmp)
            {
                var p00 = Project(new NumVec2(0, 0), player, center, scale);
                var p10 = Project(new NumVec2(t.Width, 0), player, center, scale);
                var p01 = Project(new NumVec2(0, t.Height), player, center, scale);
                var ex = (p10 - p00) / t.Width;
                var ey = (p01 - p00) / t.Height;
                var prev = rt.Transform;
                rt.Transform = new Matrix3x2(ex.X, ex.Y, ey.X, ey.Y, p00.X, p00.Y);
                rt.DrawBitmap(bmp, 1f, BitmapInterpolationMode.Linear, new Rect(0, 0, t.Width, t.Height));
                rt.Transform = prev;
            }
        }

        // Entity dots. Props (Object/Other) and dead monsters are filtered out — they're the
        // clutter; the API still serves them for troubleshooting. Game-flagged POIs (entities
        // with a MinimapIcon component) always draw with a white ring, even if their category
        // would otherwise be filtered (waypoints, checkpoints, shrines, …).
        foreach (var e in ctx.Entities)
        {
            ID2D1SolidColorBrush brush; float r;
            switch (e.Category)
            {
                case Poe2Live.EntityCategory.Monster:
                    if (!e.IsAlive) continue;            // skip corpses
                    (brush, r) = e.Rarity switch          // color + size by rarity
                    {
                        Poe2Live.Rarity.Unique => (_bUnique!, 5.5f),
                        Poe2Live.Rarity.Rare   => (_bRare!, 4.2f),
                        Poe2Live.Rarity.Magic  => (_bMagic!, 3.3f),
                        _                      => (_bMonster!, 2.8f),
                    };
                    break;
                case Poe2Live.EntityCategory.Player:     brush = _bPlayer!; r = 3.0f; break;
                case Poe2Live.EntityCategory.Npc:        brush = _bNpc!;    r = 3.5f; break;
                case Poe2Live.EntityCategory.Chest:
                    if (e.Opened) continue;                                   // skip used chests
                    if (e.Rarity is not (Poe2Live.Rarity.Rare or Poe2Live.Rarity.Unique)) continue; // rare+ only
                    (brush, r) = e.Rarity == Poe2Live.Rarity.Unique ? (_bUnique!, 4.5f) : (_bRare!, 4.0f);
                    break;
                case Poe2Live.EntityCategory.Transition: brush = _bTrans!;  r = 3.5f; break;
                default:
                    if (!e.Poi) continue;                // Object/Other → skip unless a POI
                    brush = _bObject!; r = 3.0f; break;
            }
            var p = Project(new NumVec2(e.Grid.X, e.Grid.Y), player, center, scale);
            rt.FillEllipse(new Ellipse(p, r, r), brush);
            if (e.Poi) rt.DrawEllipse(new Ellipse(p, r + 2.5f, r + 2.5f), _bText!, 1.4f); // POI ring
        }

        // Static tile landmarks (boss arena, treasure, …) — diamond + label at the group centroid.
        foreach (var lm in ctx.Landmarks)
        {
            var p = Project(new NumVec2(lm.Center.X, lm.Center.Y), player, center, scale);
            var d = 5f;
            var diamond = new[] { new NumVec2(p.X, p.Y - d), new NumVec2(p.X + d, p.Y), new NumVec2(p.X, p.Y + d), new NumVec2(p.X - d, p.Y) };
            for (var i = 0; i < 4; i++) rt.DrawLine(diamond[i], diamond[(i + 1) % 4], _bLandmark!, 1.6f);
            rt.DrawText(lm.Name, _tf!, new Rect(p.X + 7, p.Y - 7, p.X + 240, p.Y + 9), _bLandmark!, DrawTextOptions.Clip);
        }

        // Player blip on top.
        rt.FillEllipse(new Ellipse(center, 5f, 5f), _bPlayer!);
    }

    private static NumVec2 Project(NumVec2 cell, NumVec2 player, NumVec2 center, float scale)
    {
        var d = cell - player;
        var md = MapProjection.GridDeltaToMapDelta(new GameVec2 { X = d.X, Y = d.Y }, scale);
        return new NumVec2(center.X + md.X, center.Y + md.Y);
    }

    public void Dispose()
    {
        _bPlayer?.Dispose(); _bMonster?.Dispose(); _bNpc?.Dispose(); _bChest?.Dispose();
        _bTrans?.Dispose(); _bObject?.Dispose(); _bOther?.Dispose(); _bText?.Dispose(); _bPanel?.Dispose(); _bLandmark?.Dispose();
        _bMagic?.Dispose(); _bRare?.Dispose(); _bUnique?.Dispose();
        _tf?.Dispose();
        _terrain?.Dispose();
    }
}
