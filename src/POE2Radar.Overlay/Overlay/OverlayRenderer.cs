using System.Numerics;
using POE2Radar.Core.Game;
using static POE2Radar.Core.Game.JunkFilter;
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
    private static readonly Color4 ColCheatOn  = new(0.30f, 1.00f, 0.30f, 1.00f);
    private static readonly Color4 ColCheatOff = new(0.50f, 0.50f, 0.50f, 0.80f);
    private static readonly Color4 ColCheatMiss = new(0.60f, 0.25f, 0.25f, 0.70f);
    private static readonly Color4 ColLandmark = new(0.95f, 0.35f, 0.95f, 1f); // magenta — static tile landmarks

    private readonly OverlayWindow _window;
    private TerrainBitmap? _terrain;

    private enum Icon { Circle, Triangle, Star, Diamond, Plus, Square }
    private ID2D1PathGeometry? _geoTriangle, _geoStar, _geoDiamond, _geoPlus;

    private ID2D1SolidColorBrush? _bPlayer, _bMonster, _bNpc, _bChest, _bTrans, _bObject, _bOther, _bText, _bPanel, _bLandmark;
    private ID2D1SolidColorBrush? _bMagic, _bRare, _bUnique;
    private ID2D1SolidColorBrush? _bCheatOn, _bCheatOff, _bCheatMiss;
    private IDWriteTextFormat? _tf;
    private IDWriteTextFormat? _tfLandmark, _tfTransition, _tfChest;
    private float _lastLmFs, _lastTrFs, _lastChFs;
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
        _bCheatOn  = rt.CreateSolidColorBrush(ColCheatOn);
        _bCheatOff = rt.CreateSolidColorBrush(ColCheatOff);
        _bCheatMiss = rt.CreateSolidColorBrush(ColCheatMiss);
        _tf = _window.DWriteFactory.CreateTextFormat("Consolas", null, FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, FontStretch.Normal, 12f, "en-us");
        _ready = true;
    }

    private IDWriteTextFormat GetTextFormat(float size, ref IDWriteTextFormat? cached, ref float lastSize)
    {
        if (cached != null && Math.Abs(lastSize - size) < 0.1f) return cached;
        cached?.Dispose();
        cached = _window.DWriteFactory.CreateTextFormat("Consolas", null, FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, FontStretch.Normal, size, "en-us");
        lastSize = size;
        return cached;
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
            if (!ctx.OverlayVisible) { /* cleared frame = hidden */ }
            else
            {
                DrawStatus(rt, ctx);
                if (ctx.InGame && ctx.Radar?.ShowNameplates != false) DrawNameplates(rt, ctx);
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
            ? "waiting for in-game..."
            : $"{ctx.AreaCode}  HP {ctx.HpPct:F0}%  MP {ctx.ManaPct:F0}%  "
              + $"flask:{ctx.FlaskNote}  enemies:{enemies} (R{rares} U{uniques})";
        rt.FillRectangle(new Vortice.RawRectF(6, 6, 6 + line.Length * 7.3f + 10, 26), _bPanel!);
        rt.DrawText(line, _tf!, new Rect(12, 8, 1200, 22), _bText!, DrawTextOptions.Clip);

        if (ctx.CheatStatus is { Count: > 0 } cheats)
        {
            var cx = 12f;
            const float cy = 30f;
            var label = "cheats: ";
            rt.FillRectangle(new Vortice.RawRectF(6, 28, 500, 48), _bPanel!);
            rt.DrawText(label, _tf!, new Rect(cx, cy, cx + 200, cy + 14), _bText!, DrawTextOptions.Clip);
            cx += label.Length * 7.3f;

            foreach (var (_, info) in cheats)
            {
                var tag = info.Active ? "ON" : info.Found ? "off" : "--";
                var brush = info.Active ? _bCheatOn! : info.Found ? _bCheatOff! : _bCheatMiss!;
                var text = $"{info.ShortName}[{tag}] ";
                rt.DrawText(text, _tf!, new Rect(cx, cy, cx + 200, cy + 14), brush, DrawTextOptions.Clip);
                cx += text.Length * 7.3f;
            }
        }
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

    private void EnsureShapeGeometries()
    {
        if (_geoTriangle is not null) return;
        var factory = (ID2D1Factory)_window.RenderTarget.Factory;

        _geoTriangle = factory.CreatePathGeometry();
        using (var s = _geoTriangle.Open())
        {
            s.BeginFigure(new NumVec2(0f, -1f), FigureBegin.Filled);
            s.AddLine(new NumVec2(0.866f, 0.5f)); s.AddLine(new NumVec2(-0.866f, 0.5f));
            s.EndFigure(FigureEnd.Closed); s.Close();
        }
        _geoDiamond = factory.CreatePathGeometry();
        using (var s = _geoDiamond.Open())
        {
            s.BeginFigure(new NumVec2(0f, -1f), FigureBegin.Filled);
            s.AddLine(new NumVec2(1f, 0f)); s.AddLine(new NumVec2(0f, 1f)); s.AddLine(new NumVec2(-1f, 0f));
            s.EndFigure(FigureEnd.Closed); s.Close();
        }
        _geoStar = factory.CreatePathGeometry();
        using (var s = _geoStar.Open())
        {
            const float inner = 0.42f; var first = true;
            for (var i = 0; i < 10; i++)
            {
                var a = -MathF.PI / 2f + i * MathF.PI / 5f;
                var rr = (i & 1) == 0 ? 1f : inner;
                var pt = new NumVec2(MathF.Cos(a) * rr, MathF.Sin(a) * rr);
                if (first) { s.BeginFigure(pt, FigureBegin.Filled); first = false; } else s.AddLine(pt);
            }
            s.EndFigure(FigureEnd.Closed); s.Close();
        }
        _geoPlus = factory.CreatePathGeometry();
        using (var s = _geoPlus.Open())
        {
            const float a = 0.36f;
            var pts = new[] {
                new NumVec2(-a,-1f), new NumVec2(a,-1f), new NumVec2(a,-a), new NumVec2(1f,-a),
                new NumVec2(1f,a), new NumVec2(a,a), new NumVec2(a,1f), new NumVec2(-a,1f),
                new NumVec2(-a,a), new NumVec2(-1f,a), new NumVec2(-1f,-a), new NumVec2(-a,-a) };
            s.BeginFigure(pts[0], FigureBegin.Filled);
            for (var i = 1; i < pts.Length; i++) s.AddLine(pts[i]);
            s.EndFigure(FigureEnd.Closed); s.Close();
        }
    }

    /// <summary>Draw a categorical icon at screen point p with radius r. Circle/Square use D2D
    /// primitives; the rest stamp a cached unit geometry via a per-call scale+translate transform.</summary>
    private void DrawIcon(ID2D1RenderTarget rt, Icon icon, NumVec2 p, float r, ID2D1SolidColorBrush brush, bool filled)
    {
        if (icon == Icon.Circle) { if (filled) rt.FillEllipse(new Ellipse(p, r, r), brush); else rt.DrawEllipse(new Ellipse(p, r, r), brush, 1.2f); return; }
        if (icon == Icon.Square)
        {
            var h = r * 0.9f; var rect = new Vortice.RawRectF(p.X - h, p.Y - h, p.X + h, p.Y + h);
            if (filled) rt.FillRectangle(rect, brush); else rt.DrawRectangle(rect, brush, 1.5f); return;
        }
        EnsureShapeGeometries();
        var geo = icon switch { Icon.Triangle => _geoTriangle, Icon.Star => _geoStar, Icon.Diamond => _geoDiamond, Icon.Plus => _geoPlus, _ => null };
        if (geo is null) return;
        var prev = rt.Transform;
        rt.Transform = new Matrix3x2(r, 0f, 0f, r, p.X, p.Y);
        if (filled) rt.FillGeometry(geo, brush); else rt.DrawGeometry(geo, brush, 1.5f / r);
        rt.Transform = prev;
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
        var rs = ctx.Radar;
        var hideJunk = rs?.HideJunkEntities ?? true;
        ctx.EntityScreenPositions?.Clear();
        foreach (var e in ctx.Entities)
        {
            if (hideJunk && JunkFilter.IsJunk(e.Metadata)) continue;
            ID2D1SolidColorBrush brush; float r; Icon icon;
            switch (e.Category)
            {
                case Poe2Live.EntityCategory.Monster:
                    if (!e.IsAlive) continue;
                    (brush, r, icon) = e.Rarity switch
                    {
                        Poe2Live.Rarity.Unique when rs?.ShowUniqueMonsters != false => (_bUnique!, rs?.UniqueDotSize ?? 6.5f, Icon.Star),
                        Poe2Live.Rarity.Rare when rs?.ShowRareMonsters != false     => (_bRare!, rs?.RareDotSize ?? 5.5f, Icon.Triangle),
                        Poe2Live.Rarity.Magic when rs?.ShowMonsters != false        => (_bMagic!, rs?.MagicDotSize ?? 3.4f, Icon.Diamond),
                        _ when rs?.ShowMonsters != false                            => (_bMonster!, rs?.MonsterDotSize ?? 2.6f, Icon.Circle),
                        _ => default,
                    };
                    if (brush == null!) continue;
                    break;
                case Poe2Live.EntityCategory.Player:
                    if (rs?.ShowPlayers == false) continue;
                    (brush, r, icon) = (_bPlayer!, 3.0f, Icon.Circle); break;
                case Poe2Live.EntityCategory.Npc:
                    if (rs?.ShowNpcs == false) continue;
                    (brush, r, icon) = (_bNpc!, rs?.NpcDotSize ?? 4.0f, Icon.Plus); break;
                case Poe2Live.EntityCategory.Chest:
                    if (rs?.ShowChests == false) continue;
                    if (e.Opened) continue;
                    if (e.Rarity is not (Poe2Live.Rarity.Rare or Poe2Live.Rarity.Unique)) continue;
                    (brush, r, icon) = (e.Rarity == Poe2Live.Rarity.Unique ? _bUnique! : _bRare!, rs?.ChestDotSize ?? 5.0f, Icon.Square);
                    break;
                case Poe2Live.EntityCategory.Transition:
                    if (rs?.ShowTransitions == false) continue;
                    (brush, r, icon) = (_bTrans!, rs?.TransitionDotSize ?? 4.5f, Icon.Diamond); break;
                default:
                    if (!e.Poi) continue;
                    (brush, r, icon) = (_bObject!, 3.0f, Icon.Circle); break;
            }
            var p = Project(new NumVec2(e.Grid.X, e.Grid.Y), player, center, scale);
            DrawIcon(rt, icon, p, r, brush, filled: true);
            ctx.EntityScreenPositions?.Add((p.X, p.Y, e.Metadata));

            var watchMatch = ctx.Watched?.Match(e.Metadata);
            if (watchMatch is { Enabled: true })
            {
                var wr = watchMatch.Size;
                rt.FillEllipse(new Ellipse(p, wr + 2, wr + 2), _bText!);
                DrawIcon(rt, icon, p, wr, brush, filled: true);
                var wFs = rs?.WatchedFontSize ?? 14f;
                var wTf = GetTextFormat(wFs, ref _tfLandmark, ref _lastLmFs);
                rt.DrawText(watchMatch.Label, wTf, new Rect(p.X + wr + 4, p.Y - wFs / 2, p.X + 300, p.Y + wFs), _bText!);
            }
            else if (e.Category == Poe2Live.EntityCategory.Transition && rs?.ShowTransitions != false)
            {
                var trFs = rs?.TransitionFontSize ?? 12f;
                var trTf = GetTextFormat(trFs, ref _tfTransition, ref _lastTrFs);
                rt.DrawText(e.Metadata.Split('/')[^1], trTf, new Rect(p.X + r + 3, p.Y - trFs / 2, p.X + 250, p.Y + trFs), _bTrans!);
            }
            else if (e.Category == Poe2Live.EntityCategory.Chest && rs?.ShowChests != false)
            {
                var chFs = rs?.ChestFontSize ?? 12f;
                var chTf = GetTextFormat(chFs, ref _tfChest, ref _lastChFs);
                var tag = e.Rarity == Poe2Live.Rarity.Unique ? "[U]" : "[R]";
                rt.DrawText(tag, chTf, new Rect(p.X + r + 3, p.Y - chFs / 2, p.X + 100, p.Y + chFs), brush);
            }
        }

        if (rs?.ShowLandmarks == false) goto skipLandmarks;
        var lmFs = rs?.LandmarkFontSize ?? 12f;
        var lmTf = GetTextFormat(lmFs, ref _tfLandmark, ref _lastLmFs);
        ctx.LandmarkScreenPositions?.Clear();
        foreach (var lm in ctx.Landmarks)
        {
            var p = Project(new NumVec2(lm.Center.X, lm.Center.Y), player, center, scale);
            var d = 5f;
            var diamond = new[] { new NumVec2(p.X, p.Y - d), new NumVec2(p.X + d, p.Y), new NumVec2(p.X, p.Y + d), new NumVec2(p.X - d, p.Y) };
            for (var i = 0; i < 4; i++) rt.DrawLine(diamond[i], diamond[(i + 1) % 4], _bLandmark!, 1.6f);
            rt.DrawText(lm.Name, lmTf, new Rect(p.X + 7, p.Y - lmFs / 2, p.X + 300, p.Y + lmFs), _bLandmark!);
            ctx.LandmarkScreenPositions?.Add((p.X, p.Y, lm.Center.X, lm.Center.Y, lm.Name));
        }

        skipLandmarks:

        // Pathfinding line
        if (ctx.PathPoints is { Count: >= 2 } pp)
        {
            for (var i = 0; i < pp.Count - 1; i++)
            {
                var a = Project(new NumVec2(pp[i].X, pp[i].Y), player, center, scale);
                var b = Project(new NumVec2(pp[i + 1].X, pp[i + 1].Y), player, center, scale);
                rt.DrawLine(a, b, _bTrans!, rs?.PathWidth ?? 2.5f);
            }
            var end = Project(new NumVec2(pp[^1].X, pp[^1].Y), player, center, scale);
            rt.FillEllipse(new Ellipse(end, 5f, 5f), _bTrans!);
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
        _bCheatOn?.Dispose(); _bCheatOff?.Dispose(); _bCheatMiss?.Dispose();
        _geoTriangle?.Dispose(); _geoStar?.Dispose(); _geoDiamond?.Dispose(); _geoPlus?.Dispose();
        _tf?.Dispose(); _tfLandmark?.Dispose(); _tfTransition?.Dispose(); _tfChest?.Dispose();
        _terrain?.Dispose();
    }
}
