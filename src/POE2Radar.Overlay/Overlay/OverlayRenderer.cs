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

    private static readonly Dictionary<Poe2Live.LeagueMechanic, (Color4 Color, string Label)> LeagueStyles = new()
    {
        [Poe2Live.LeagueMechanic.Expedition]  = (new Color4(0.90f, 0.75f, 0.30f, 1f), "Exped"),
        [Poe2Live.LeagueMechanic.Breach]      = (new Color4(0.70f, 0.20f, 0.90f, 1f), "Breach"),
        [Poe2Live.LeagueMechanic.Ritual]      = (new Color4(0.85f, 0.15f, 0.15f, 1f), "Ritual"),
        [Poe2Live.LeagueMechanic.Delirium]    = (new Color4(0.75f, 0.75f, 0.75f, 1f), "Delir"),
        [Poe2Live.LeagueMechanic.Abyss]       = (new Color4(0.30f, 0.85f, 0.30f, 1f), "Abyss"),
        [Poe2Live.LeagueMechanic.Incursion]   = (new Color4(1.00f, 0.50f, 0.20f, 1f), "Incur"),
        [Poe2Live.LeagueMechanic.Legion]      = (new Color4(0.60f, 0.40f, 0.20f, 1f), "Legion"),
        [Poe2Live.LeagueMechanic.Betrayal]    = (new Color4(0.40f, 0.70f, 0.40f, 1f), "Betray"),
        [Poe2Live.LeagueMechanic.Ultimatum]   = (new Color4(0.90f, 0.20f, 0.40f, 1f), "Ultim"),
        [Poe2Live.LeagueMechanic.Sanctum]     = (new Color4(0.50f, 0.80f, 1.00f, 1f), "Sanct"),
        [Poe2Live.LeagueMechanic.Delve]       = (new Color4(0.20f, 0.60f, 0.90f, 1f), "Delve"),
        [Poe2Live.LeagueMechanic.Heist]       = (new Color4(0.90f, 0.40f, 0.60f, 1f), "Heist"),
        [Poe2Live.LeagueMechanic.Blight]      = (new Color4(0.50f, 0.80f, 0.20f, 1f), "Blight"),
        [Poe2Live.LeagueMechanic.Hellscape]   = (new Color4(0.90f, 0.30f, 0.10f, 1f), "Hell"),
    };
    private readonly Dictionary<Poe2Live.LeagueMechanic, ID2D1SolidColorBrush> _leagueBrushes = new();
    private string _lastLandmarkColor = "";

    private readonly OverlayWindow _window;
    private TerrainBitmap? _terrain;

    private enum Icon { Circle, Triangle, Star, Diamond, Plus, Square }
    private ID2D1PathGeometry? _geoTriangle, _geoStar, _geoDiamond, _geoPlus;

    // SVG icon library geometry cache (built lazily from IconLibrary, keyed by icon name)
    private readonly Dictionary<string, ID2D1PathGeometry?> _geoCache = new(StringComparer.OrdinalIgnoreCase);

    private ID2D1SolidColorBrush? _bPlayer, _bMonster, _bNpc, _bChest, _bTrans, _bObject, _bOther, _bText, _bPanel, _bLandmark;
    private ID2D1SolidColorBrush? _bMagic, _bRare, _bUnique;
    private ID2D1SolidColorBrush? _bCheatOn, _bCheatOff, _bCheatMiss, _bFog, _bRing, _bOutline, _bFriendly;
    private ID2D1SolidColorBrush? _bStyle; // scratch brush recolored per-draw for config-driven icons
    private IDWriteTextFormat? _tf;
    private IDWriteTextFormat? _tfLandmark, _tfTransition, _tfChest, _tfStatus;
    private float _lastLmFs, _lastTrFs, _lastChFs, _lastStatusFs;
    private string _lastFont = "";
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
        _bLandmark = rt.CreateSolidColorBrush(new Color4(0.95f, 0.35f, 0.95f, 1f));
        _bMagic   = rt.CreateSolidColorBrush(ColMagic);
        _bRare    = rt.CreateSolidColorBrush(ColRare);
        _bUnique  = rt.CreateSolidColorBrush(ColUnique);
        _bCheatOn  = rt.CreateSolidColorBrush(ColCheatOn);
        _bCheatOff = rt.CreateSolidColorBrush(ColCheatOff);
        _bCheatMiss = rt.CreateSolidColorBrush(ColCheatMiss);
        _bOutline = rt.CreateSolidColorBrush(new Color4(0f, 0f, 0f, 0.8f));
        _bFriendly = rt.CreateSolidColorBrush(new Color4(0.2f, 0.9f, 0.3f, 0.9f));
        _bStyle = rt.CreateSolidColorBrush(ColText);
        _tf = _window.DWriteFactory.CreateTextFormat("Consolas", null, FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, FontStretch.Normal, 12f, "en-us");
        _ready = true;
    }

    private IDWriteTextFormat GetTextFormat(float size, ref IDWriteTextFormat? cached, ref float lastSize, string? font = null)
    {
        font ??= _lastFont;
        if (cached != null && Math.Abs(lastSize - size) < 0.1f && font == _lastFont) return cached;
        cached?.Dispose();
        cached = _window.DWriteFactory.CreateTextFormat(font, null, FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, FontStretch.Normal, size, "en-us");
        lastSize = size;
        return cached;
    }

    public void Render(RenderContext ctx)
    {
        if (!_window.IsValid) return;
        EnsureResources();
        var font = ctx.Radar?.FontFamily ?? "Consolas";
        if (font != _lastFont)
        {
            _lastFont = font;
            _tfLandmark?.Dispose(); _tfLandmark = null;
            _tfTransition?.Dispose(); _tfTransition = null;
            _tfChest?.Dispose(); _tfChest = null;
            _tfStatus?.Dispose(); _tfStatus = null;
            _tf?.Dispose();
            _tf = _window.DWriteFactory.CreateTextFormat(font, null, FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, FontStretch.Normal, 12f, "en-us");
        }
        var rt = _window.RenderTarget;
        rt.BeginDraw();
        rt.Clear(new Color4(0f, 0f, 0f, 0f));
        rt.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Grayscale;
        try
        {
            // Draw nothing unless overlay is enabled AND PoE2 is the foreground window —
            // so the overlay auto-hides when you alt-tab. (The cleared frame above hides prior content.)
            if (!ctx.OverlayVisible || !ctx.Active) { /* cleared frame = hidden */ }
            else
            {
                DrawStatus(rt, ctx);
                if (ctx.InGame && ctx.Radar?.ShowNameplates != false) DrawNameplates(rt, ctx);
                if (ctx is { InGame: true, Map.IsVisible: true })
                    DrawMap(rt, ctx);
                if (ctx.InGame && ctx.Radar?.ShowMinimap == true && !ctx.Map.IsVisible)
                    DrawMinimap(rt, ctx);
                if (ctx.InGame && !ctx.Map.IsVisible && ctx.PathPoints is { Count: >= 2 } && ctx.Radar?.ShowPath != false && ctx.Radar?.ShowGroundWaypoints != false)
                    DrawGroundWaypoints(rt, ctx);
                if (ctx.InspectedMeta != null)
                    DrawInspector(rt, ctx);
                if (ctx.ShowZoneGuide && ctx.ZoneGuideTitle != null)
                    DrawZoneGuide(rt, ctx);
                if (ctx.PathTargetName != null)
                    DrawPathTarget(rt, ctx);
            }
        }
        finally { rt.EndDraw(); }
        _window.Present();
    }

    private void DrawStatus(ID2D1RenderTarget rt, RenderContext ctx)
    {
        if (ctx.Radar?.ShowStatusBar == false) return;

        int alive = 0, normals = 0, magics = 0, rares = 0, uniques = 0, bosses = 0;
        int npcs = 0, chests = 0, transitions = 0;
        var leagueCounts = new Dictionary<Poe2Live.LeagueMechanic, int>();
        foreach (var e in ctx.Entities)
        {
            if (e.Category == Poe2Live.EntityCategory.Monster && e.IsAlive)
            {
                alive++;
                if (e.IsBoss) bosses++;
                if (e.IsLeagueMechanic) { leagueCounts.TryGetValue(e.League, out var lc); leagueCounts[e.League] = lc + 1; }
                switch (e.Rarity) { case Poe2Live.Rarity.Unique: uniques++; break; case Poe2Live.Rarity.Rare: rares++; break; case Poe2Live.Rarity.Magic: magics++; break; default: normals++; break; }
            }
            else if (e.Category == Poe2Live.EntityCategory.Npc) npcs++;
            else if (e.Category == Poe2Live.EntityCategory.Chest && !e.Opened) chests++;
            else if (e.Category == Poe2Live.EntityCategory.Transition) transitions++;
        }

        var fs = ctx.Radar?.StatusFontSize ?? 12f;
        var stf = GetTextFormat(fs, ref _tfStatus, ref _lastStatusFs);
        var cw = fs * 0.6f;
        var lh = fs + 4f;

        var zoneName = ctx.AreaName ?? ctx.AreaCode;
        var townTag = ctx.IsTown ? " [Town]" : "";
        var zoneInfo = ctx.AreaAct > 0 ? $"{zoneName} (Act {ctx.AreaAct}){townTag}" : $"{zoneName}{townTag}";
        var line = !ctx.InGame
            ? "waiting for in-game..."
            : $"{zoneInfo}  {ctx.CharName ?? ""} Lv{ctx.CharLevel}  HP {ctx.HpPct:F0}%  MP {ctx.ManaPct:F0}%  flask:{ctx.FlaskNote}";
        rt.FillRectangle(new Vortice.RawRectF(6, 6, 6 + line.Length * cw + 10, 6 + lh), _bPanel!);
        rt.DrawText(line, stf, new Rect(12, 8, 1200, 8 + lh), _bText!, DrawTextOptions.Clip);

        if (ctx.InGame)
        {
            var hud = $"Alive:{alive} (Normal:{normals} Magic:{magics} Rare:{rares} Unique:{uniques})  NPC:{npcs}  Chest:{chests}  Exit:{transitions}";
            var hudY = 6 + lh + 2f;
            rt.FillRectangle(new Vortice.RawRectF(6, hudY, 6 + hud.Length * cw + 10, hudY + lh), _bPanel!);

            var cx = 12f;
            void DrawSeg(string t, ID2D1SolidColorBrush b) { rt.DrawText(t, stf, new Rect(cx, hudY + 2, cx + 600, hudY + lh), b); cx += t.Length * cw; }
            DrawSeg($"Alive:{alive} (", _bText!);
            DrawSeg($"Normal:{normals} ", _bMonster!);
            DrawSeg($"Magic:{magics} ", _bMagic!);
            DrawSeg($"Rare:{rares} ", _bRare!);
            DrawSeg($"Unique:{uniques}", _bUnique!);
            DrawSeg($")  ", _bText!);
            DrawSeg($"NPC:{npcs}  ", _bNpc!);
            DrawSeg($"Chest:{chests}  ", _bChest!);
            DrawSeg($"Exit:{transitions}", _bTrans!);
            if (bosses > 0) DrawSeg($"  BOSS:{bosses}", _bUnique!);
            foreach (var (league, count) in leagueCounts)
            {
                if (LeagueStyles.TryGetValue(league, out var style))
                    DrawSeg($"  {style.Label}:{count}", GetLeagueBrush(rt, league));
            }
        }

        if (ctx.CheatStatus is { Count: > 0 } cheats)
        {
            var cx = 12f;
            var cy = 6 + lh * 2 + 4f;
            var label = "cheats: ";
            rt.FillRectangle(new Vortice.RawRectF(6, cy - 2, 500, cy + lh), _bPanel!);
            rt.DrawText(label, stf, new Rect(cx, cy, cx + 200, cy + lh), _bText!, DrawTextOptions.Clip);
            cx += label.Length * cw;

            foreach (var (_, info) in cheats)
            {
                var tag = info.Active ? "ON" : info.Found ? "off" : "--";
                var brush = info.Active ? _bCheatOn! : info.Found ? _bCheatOff! : _bCheatMiss!;
                var text = $"{info.ShortName}[{tag}] ";
                rt.DrawText(text, stf, new Rect(cx, cy, cx + 200, cy + lh), brush, DrawTextOptions.Clip);
                cx += text.Length * cw;
            }
        }

        // POI IN MAP panel — landmarks, exits, quest pins, bosses — all at a glance below cheats
        if (ctx.InGame)
        {
            var poiY = 6 + lh * 3 + 8f;
            var poiFs = fs * 0.9f;
            var poiTf = GetTextFormat(poiFs, ref _tfTransition, ref _lastTrFs);
            var poiCw = poiFs * 0.6f;
            var lines = new List<(string text, ID2D1SolidColorBrush brush)>();

            // Exits/transitions
            foreach (var e in ctx.Entities)
            {
                if (e.Category != Poe2Live.EntityCategory.Transition) continue;
                var trName = ctx.EntityNames?.ResolveOrShorten(e.Metadata) ?? e.Metadata.Split('/')[^1];
                var destArea = ctx.GameData?.GetArea(trName);
                var label = destArea != null ? $"→ {destArea.Name}" : $"→ {trName}";
                if (!lines.Any(l => l.text == label))
                    lines.Add((label, _bTrans!));
            }

            // Landmarks (boss arenas, quest rewards, etc.)
            var poiHidden = ctx.Hidden;
            foreach (var lm in ctx.Landmarks)
            {
                if (poiHidden != null && (poiHidden.IsHidden(lm.Name) || poiHidden.IsHidden(lm.Path))) continue;
                lines.Add(($"◆ {lm.Name}", _bLandmark!));
            }

            // Quest pins
            if (ctx.MapPins is { Count: > 0 } pins)
            {
                foreach (var pin in pins)
                {
                    if (string.IsNullOrEmpty(pin.Name) || pin.Name == "¢") continue;
                    lines.Add(($"● {pin.Name}", pin.Type == "quest" ? _bUnique! : _bNpc!));
                }
            }

            // Live bosses
            foreach (var e in ctx.Entities)
            {
                if (!e.IsBoss || !e.IsAlive) continue;
                var bName = ctx.EntityNames?.ResolveOrShorten(e.Metadata) ?? e.Metadata.Split('/')[^1];
                lines.Add(($"★ BOSS: {bName}", _bUnique!));
            }

            if (lines.Count > 0)
            {
                var maxLen = lines.Max(l => l.text.Length);
                var panelW = maxLen * poiCw + 20f;
                var panelH = lines.Count * (poiFs + 3) + 6f;
                rt.FillRectangle(new Vortice.RawRectF(6, poiY - 2, 6 + panelW, poiY + panelH), _bPanel!);
                rt.DrawText("POI IN MAP:", poiTf, new Rect(10, poiY, 200, poiY + poiFs), _bText!);
                poiY += poiFs + 3;
                foreach (var (text, brush) in lines)
                {
                    rt.DrawText(text, poiTf, new Rect(12, poiY, 12 + panelW, poiY + poiFs + 2), brush);
                    poiY += poiFs + 3;
                }
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

            var rs2 = ctx.Radar;
            var hpBars = rs2?.HpBars;
            var npScale = rs2?.NameplateBarWidth ?? 1.0f;
            var bw = e.Rarity switch
            {
                Poe2Live.Rarity.Unique => (hpBars?.WidthUnique ?? 64f) * npScale,
                Poe2Live.Rarity.Rare   => (hpBars?.WidthRare ?? 50f) * npScale,
                _                      => (hpBars?.WidthMagic ?? 38f) * npScale,
            };
            var styles2 = rs2?.Styles;
            var barStyle = e.Rarity switch
            {
                Poe2Live.Rarity.Unique => styles2?.MonsterUnique,
                Poe2Live.Rarity.Rare   => styles2?.MonsterRare,
                _                      => styles2?.MonsterMagic,
            };
            SetStyleBrush(barStyle?.Color ?? "#FF7300", barStyle?.Opacity ?? 1f);
            var col = _bStyle!;
            var bh = hpBars?.Height ?? rs2?.NameplateBarHeight ?? 5f;
            var bx = sx - bw / 2f + (hpBars?.OffsetX ?? 0f);
            var by = sy + (hpBars?.OffsetY ?? rs2?.NameplateOffsetY ?? -30f);
            var frac = e.HpFraction;
            rt.FillRectangle(new Vortice.RawRectF(bx, by, bx + bw, by + bh), _bPanel!);
            var fill = frac < 0.3f ? _bMonster! : col;
            rt.FillRectangle(new Vortice.RawRectF(bx, by, bx + bw * frac, by + bh), fill);
            rt.DrawRectangle(new Vortice.RawRectF(bx, by, bx + bw, by + bh), col, 1f);
        }
    }

    private void DrawGroundWaypoints(ID2D1RenderTarget rt, RenderContext ctx)
    {
        if (ctx.CameraMatrix is not { } m || ctx.PathPoints is not { Count: >= 2 } pp) return;
        float W = ctx.WindowWidth, H = ctx.WindowHeight;
        var ratio = Poe2.WorldToGridRatio;
        var pathBrush = _bTrans!;

        // Draw every Nth point as a ground marker (skip dense points for perf)
        var step = Math.Max(1, pp.Count / 40);
        NumVec2? prevScreen = null;

        for (var i = 0; i < pp.Count; i += step)
        {
            var wx = pp[i].X * ratio;
            var wy = pp[i].Y * ratio;
            float wz = 0;

            var cw = wx * m[3] + wy * m[7] + wz * m[11] + m[15];
            if (cw <= 0.001f) continue;
            var cx = wx * m[0] + wy * m[4] + wz * m[8] + m[12];
            var cy = wx * m[1] + wy * m[5] + wz * m[9] + m[13];
            var sx = (cx / cw / 2f + 0.5f) * W;
            var sy = (0.5f - cy / cw / 2f) * H;
            if (sx < -50 || sx > W + 50 || sy < -50 || sy > H + 50) continue;

            var sp = new NumVec2(sx, sy);

            if (prevScreen.HasValue)
                rt.DrawLine(prevScreen.Value, sp, pathBrush, 2f);

            var dotR = i == pp.Count - 1 || i >= pp.Count - step ? 6f : 3f;
            rt.FillEllipse(new Ellipse(sp, dotR, dotR), pathBrush);
            prevScreen = sp;
        }

        // Draw destination marker larger
        var last = pp[^1];
        var lwx = last.X * ratio; var lwy = last.Y * ratio;
        var lcw = lwx * m[3] + lwy * m[7] + m[15];
        if (lcw > 0.001f)
        {
            var lsx = (lwx * m[0] + lwy * m[4] + m[12]) / lcw / 2f + 0.5f;
            var lsy = 0.5f - (lwx * m[1] + lwy * m[5] + m[13]) / lcw / 2f;
            var destP = new NumVec2(lsx * W, lsy * H);
            if (destP.X > 0 && destP.X < W && destP.Y > 0 && destP.Y < H)
            {
                rt.DrawEllipse(new Ellipse(destP, 10f, 10f), pathBrush, 2f);
                rt.FillEllipse(new Ellipse(destP, 5f, 5f), pathBrush);
            }
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

    private ID2D1PathGeometry? GetLibraryGeometry(string name)
    {
        if (_geoCache.TryGetValue(name, out var cached)) return cached;
        if (!IconLibrary.Map.TryGetValue(name, out var def)) { _geoCache[name] = null; return null; }
        var factory = (ID2D1Factory)_window.RenderTarget.Factory;
        var geo = factory.CreatePathGeometry();
        using var sink = geo.Open();
        foreach (var pathD in def.Paths)
        {
            var figs = SvgPath.Parse(pathD);
            foreach (var fig in figs)
            {
                var ox = def.VbX + def.VbW / 2f;
                var oy = def.VbY + def.VbH / 2f;
                var scale = 2f / MathF.Max(def.VbW, def.VbH);
                NumVec2 Norm(System.Numerics.Vector2 v) => new((v.X - ox) * scale, (v.Y - oy) * scale);

                sink.BeginFigure(Norm(fig.Start), FigureBegin.Filled);
                foreach (var seg in fig.Segs)
                {
                    switch (seg.Kind)
                    {
                        case SvgPath.SegKind.Line:
                            sink.AddLine(Norm(seg.End)); break;
                        case SvgPath.SegKind.Cubic:
                            sink.AddBezier(new BezierSegment(Norm(seg.C1), Norm(seg.C2), Norm(seg.End))); break;
                        case SvgPath.SegKind.Quad:
                            sink.AddQuadraticBezier(new QuadraticBezierSegment { Point1 = Norm(seg.C1), Point2 = Norm(seg.End) }); break;
                    }
                }
                sink.EndFigure(fig.Closed ? FigureEnd.Closed : FigureEnd.Open);
            }
        }
        sink.Close();
        _geoCache[name] = geo;
        return geo;
    }

    private void DrawStyledIcon(ID2D1RenderTarget rt, string shapeName, NumVec2 p, float r, ID2D1SolidColorBrush brush, bool filled = true)
    {
        if (string.Equals(shapeName, "Circle", StringComparison.OrdinalIgnoreCase))
        {
            if (filled) rt.FillEllipse(new Ellipse(p, r, r), brush);
            else rt.DrawEllipse(new Ellipse(p, r, r), brush, 1.2f);
            return;
        }
        if (string.Equals(shapeName, "Square", StringComparison.OrdinalIgnoreCase))
        {
            var h = r * 0.9f; var rect = new Vortice.RawRectF(p.X - h, p.Y - h, p.X + h, p.Y + h);
            if (filled) rt.FillRectangle(rect, brush); else rt.DrawRectangle(rect, brush, 1.5f);
            return;
        }
        var geo = GetLibraryGeometry(shapeName);
        if (geo is null) { rt.FillEllipse(new Ellipse(p, r, r), brush); return; }
        var prev = rt.Transform;
        rt.Transform = new Matrix3x2(r, 0f, 0f, r, p.X, p.Y);
        if (filled) rt.FillGeometry(geo, brush); else rt.DrawGeometry(geo, brush, 1.5f / r);
        rt.Transform = prev;
    }

    private void SetStyleBrush(IconStyle style)
    {
        if (_bStyle == null) return;
        ParseHex(style.Color, out var r, out var g, out var b);
        _bStyle.Color = new Color4(r / 255f, g / 255f, b / 255f, style.Opacity);
    }

    private void SetStyleBrush(string hexColor, float opacity)
    {
        if (_bStyle == null) return;
        ParseHex(hexColor, out var r, out var g, out var b);
        _bStyle.Color = new Color4(r / 255f, g / 255f, b / 255f, opacity);
    }

    private MechanicStyle? MatchMechanic(RadarStyles styles, string metadata)
    {
        foreach (var m in styles.Mechanics)
        {
            if (!m.Enabled) continue;
            foreach (var pat in m.Match)
                if (metadata.Contains(pat, StringComparison.OrdinalIgnoreCase)) return m;
        }
        return null;
    }

    private void DrawMap(ID2D1RenderTarget rt, RenderContext ctx)
    {
        // MapCenter = window center + DefaultShift(0,-20) + Shift + manual offset.
        var center = new NumVec2(
            ctx.WindowWidth  * 0.5f + ctx.Map.ShiftX + ctx.OffsetX,
            ctx.WindowHeight * 0.5f + ctx.Map.ShiftY + (ctx.Radar?.MapCenterYShift ?? -20f) + ctx.OffsetY);
        var scale = ctx.Map.Zoom * (ctx.WindowHeight / 677f) * ctx.ScaleMul;
        var player = ctx.PlayerGrid;

        // Terrain bitmap, projected via the same affine grid→screen transform.
        if (ctx.Terrain is { } t)
        {
            _terrain ??= new TerrainBitmap(rt);
            var ts = ctx.Radar?.Terrain;
            ParseHex(ts?.EdgeColor ?? ctx.Radar?.TerrainEdgeColor ?? "#3cdcff", out var eR, out var eG, out var eB);
            var eA = (byte)Math.Clamp((int)((ts?.EdgeOpacity ?? ctx.Radar?.TerrainEdgeAlpha ?? 0.7f) * 255), 0, 255);
            ParseHex(ts?.InteriorColor ?? "#506482", out var iR, out var iG, out var iB);
            var iA = (byte)Math.Clamp((int)((ts?.InteriorOpacity ?? ctx.Radar?.TerrainInteriorAlpha ?? 0.12f) * 255), 0, 255);
            _terrain.EnsureBuiltRaw(t.Walkable, t.Width, t.Height, ctx.AreaHash, inTransition: false,
                edgeR: eR, edgeG: eG, edgeB: eB, edgeAlpha: eA, interiorAlpha: iA);
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

        // Exploration fog — dim unexplored walkable areas
        if (ctx is { Terrain: { } ft, Exploration: { } expl, Radar.ShowExplorationFog: true })
        {
            var fogAlpha = ctx.Radar?.FogOpacity ?? 0.45f;
            if (_bFog == null)
                _bFog = rt.CreateSolidColorBrush(new Color4(0f, 0f, 0f, fogAlpha));
            else
                _bFog.Color = new Color4(0f, 0f, 0f, fogAlpha);

            var step = ctx.Radar?.FogGridStep ?? 4;
            var fogScale = ctx.Radar?.FogCellScale ?? 0.12f;
            var halfStep = step / 2f;
            for (var gy = 0; gy < ft.Height; gy += step)
            {
                for (var gx = 0; gx < ft.Width; gx += step)
                {
                    if (ft.Walkable[gy * ft.Width + gx] == 0) continue;
                    if (expl.IsExplored(gx, gy)) continue;
                    var p = Project(new NumVec2(gx + halfStep, gy + halfStep), player, center, scale);
                    var sz = scale * step * fogScale;
                    if (sz < 0.5f) continue;
                    rt.FillRectangle(new Vortice.RawRectF(p.X - sz, p.Y - sz, p.X + sz, p.Y + sz), _bFog);
                }
            }
        }

        var rs = ctx.Radar;
        var hideJunk = rs?.HideJunkEntities ?? true;
        var hidden = ctx.Hidden;
        ctx.EntityScreenPositions?.Clear();
        foreach (var e in ctx.Entities)
        {
            if (hideJunk && JunkFilter.IsJunk(e.Metadata)) continue;
            if (hidden != null && hidden.IsHidden(e.Metadata)) continue;
            var drawRange = rs?.EntityDrawRange ?? 0f;
            if (drawRange > 0 && e.Category != Poe2Live.EntityCategory.Transition)
            {
                var dx = e.Grid.X - player.X; var dy = e.Grid.Y - player.Y;
                if (dx * dx + dy * dy > drawRange * drawRange) continue;
            }
            if (!e.IsTargetable && rs?.HideUntargetable == true) continue;
            if (e.IsFriendly && e.Category == Poe2Live.EntityCategory.Monster && rs?.ShowFriendlyEntities == false) continue;
            if (e.IsImmobile && e.Category == Poe2Live.EntityCategory.Monster && rs?.ShowImmobileEntities == false) continue;
            var minHp = rs?.MinEntityHpPct ?? 0f;
            if (minHp > 0 && e.HasLife && e.IsAlive && e.HpFraction * 100f < minHp) continue;
            var styles = rs?.Styles;
            string shapeName; float r; ID2D1SolidColorBrush brush;

            // Check mechanic overrides first (metadata-matched, first enabled match wins)
            var mechMatch = styles != null ? MatchMechanic(styles, e.Metadata) : null;
            if (mechMatch != null)
            {
                SetStyleBrush(mechMatch.Color, mechMatch.Opacity);
                (shapeName, r, brush) = (mechMatch.Shape, mechMatch.Size, _bStyle!);
            }
            else switch (e.Category)
            {
                case Poe2Live.EntityCategory.Monster:
                    if (!e.IsAlive && rs?.ShowDeadMonsters != true) continue;
                    if (e.IsBoss && rs?.ShowBossHighlight != false && e.IsAlive)
                    {
                        var bs = styles?.MonsterUnique;
                        SetStyleBrush(bs?.Color ?? "#FF7300", bs?.Opacity ?? 1f);
                        (shapeName, r, brush) = (bs?.Shape ?? "Star", rs?.BossDotSize ?? 8.0f, _bStyle!);
                        break;
                    }
                    if (e.IsLeagueMechanic)
                    {
                        var lb = GetLeagueBrush(rt, e.League);
                        var ls = e.Rarity switch
                        {
                            Poe2Live.Rarity.Unique => styles?.MonsterUnique,
                            Poe2Live.Rarity.Rare   => styles?.MonsterRare,
                            Poe2Live.Rarity.Magic  => styles?.MonsterMagic,
                            _                      => styles?.MonsterNormal,
                        };
                        (shapeName, r, brush) = (ls?.Shape ?? "Circle", ls?.Size ?? 2.6f, lb);
                        break;
                    }
                    {
                        var ms = e.Rarity switch
                        {
                            Poe2Live.Rarity.Unique when rs?.ShowUniqueMonsters != false => styles?.MonsterUnique,
                            Poe2Live.Rarity.Rare when rs?.ShowRareMonsters != false     => styles?.MonsterRare,
                            Poe2Live.Rarity.Magic when rs?.ShowMonsters != false        => styles?.MonsterMagic,
                            _ when rs?.ShowMonsters != false && rs?.ShowNormalMonsters != false => styles?.MonsterNormal,
                            _ => null,
                        };
                        if (ms == null) continue;
                        SetStyleBrush(ms);
                        (shapeName, r, brush) = (ms.Shape, ms.Size, _bStyle!);
                    }
                    break;
                case Poe2Live.EntityCategory.Player:
                    if (rs?.ShowPlayers == false) continue;
                    { var ps = styles?.Player; SetStyleBrush(ps?.Color ?? "#4DF2FF", ps?.Opacity ?? 1f); }
                    (shapeName, r, brush) = (styles?.Player?.Shape ?? "Circle", styles?.Player?.Size ?? 5.0f, _bStyle!); break;
                case Poe2Live.EntityCategory.Npc:
                    if (rs?.ShowNpcs == false) continue;
                    { var ns = styles?.Npc; SetStyleBrush(ns?.Color ?? "#FFD933", ns?.Opacity ?? 0.95f); }
                    (shapeName, r, brush) = (styles?.Npc?.Shape ?? "Plus", styles?.Npc?.Size ?? 4.0f, _bStyle!); break;
                case Poe2Live.EntityCategory.Chest:
                    if (rs?.ShowChests == false) continue;
                    if (e.Opened) continue;
                    if (e.Rarity is not (Poe2Live.Rarity.Rare or Poe2Live.Rarity.Unique) && rs?.ShowNormalChests != true) continue;
                    {
                        var cs = e.Rarity == Poe2Live.Rarity.Unique ? styles?.ChestUnique :
                                 e.Rarity == Poe2Live.Rarity.Rare ? styles?.ChestRare : styles?.ChestRare;
                        SetStyleBrush(cs?.Color ?? "#F28C1A", cs?.Opacity ?? 0.95f);
                        (shapeName, r, brush) = (cs?.Shape ?? "Square", cs?.Size ?? 5.0f, _bStyle!);
                    }
                    break;
                case Poe2Live.EntityCategory.Transition:
                    if (rs?.ShowTransitions == false) continue;
                    { var ts = styles?.Transition; SetStyleBrush(ts?.Color ?? "#66FF99", ts?.Opacity ?? 0.95f); }
                    (shapeName, r, brush) = (styles?.Transition?.Shape ?? "Diamond", styles?.Transition?.Size ?? 4.5f, _bStyle!); break;
                default:
                    if (!e.Poi) continue;
                    { var ps = styles?.Poi; SetStyleBrush(ps?.Color ?? "#8CBFFF", ps?.Opacity ?? 0.70f); }
                    (shapeName, r, brush) = (styles?.Poi?.Shape ?? "Circle", styles?.Poi?.Size ?? 3.0f, _bStyle!); break;
            }
            if (e.Category == Poe2Live.EntityCategory.Monster && e.Scale > 1.5f)
                r *= Math.Min(e.Scale / 1.5f, 2.0f);

            if (e.Category == Poe2Live.EntityCategory.Monster && e.IsFriendly && _bFriendly != null)
                brush = _bFriendly;

            if (e.Category == Poe2Live.EntityCategory.Monster && e.BaseSpeed > 50 && string.Equals(shapeName, "Circle", StringComparison.OrdinalIgnoreCase))
                shapeName = "Diamond";

            var p = Project(new NumVec2(e.Grid.X, e.Grid.Y), player, center, scale);
            var outW = rs?.DotOutlineWidth ?? 0f;
            if (outW > 0 && _bOutline != null)
                DrawStyledIcon(rt, shapeName, p, r + outW, _bOutline, filled: true);
            DrawStyledIcon(rt, shapeName, p, r, brush, filled: true);
            ctx.EntityScreenPositions?.Add((p.X, p.Y, e.Metadata));

            var watchMatch = ctx.Watched?.Match(e.Metadata);
            if (watchMatch is { Enabled: true })
            {
                var wr = watchMatch.Size;
                rt.FillEllipse(new Ellipse(p, wr + 2, wr + 2), _bText!);
                DrawStyledIcon(rt, shapeName, p, wr, brush, filled: true);
                var wFs = rs?.WatchedFontSize ?? 14f;
                var wTf = GetTextFormat(wFs, ref _tfLandmark, ref _lastLmFs);
                rt.DrawText(watchMatch.Label, wTf, new Rect(p.X + wr + 4, p.Y - wFs / 2, p.X + 300, p.Y + wFs), _bText!);
            }
            else if (e.Category == Poe2Live.EntityCategory.Transition && rs?.ShowTransitions != false)
            {
                if (rs?.ShowTransitionLabels != false)
                {
                    var trFs = rs?.TransitionFontSize ?? 12f;
                    var trTf = GetTextFormat(trFs, ref _tfTransition, ref _lastTrFs);
                    var trName = ctx.EntityNames?.ResolveOrShorten(e.Metadata) ?? e.Metadata.Split('/')[^1];
                    var destArea = ctx.GameData?.GetArea(trName);
                    var destLabel = destArea != null ? $"→ {destArea.Name}" : trName;
                    rt.DrawText(destLabel, trTf, new Rect(p.X + r + 3, p.Y - trFs / 2, p.X + 300, p.Y + trFs), _bTrans!);
                }
            }
            else if (e.Category == Poe2Live.EntityCategory.Monster && e.IsAlive &&
                     e.Rarity is Poe2Live.Rarity.Unique or Poe2Live.Rarity.Rare)
            {
                if (rs?.ShowMonsterLabels != false)
                {
                    var mFs = rs?.NameplateFontSize ?? 12f;
                    var mTf = GetTextFormat(mFs, ref _tfChest, ref _lastChFs);
                    var mName = ctx.EntityNames?.ResolveOrShorten(e.Metadata) ?? e.Metadata.Split('/')[^1];
                    var mBrush = e.Rarity == Poe2Live.Rarity.Unique ? _bUnique! : _bRare!;
                    rt.DrawText(mName, mTf, new Rect(p.X + r + 3, p.Y - mFs / 2, p.X + 300, p.Y + mFs), mBrush);
                }
            }
            else if (e.Category == Poe2Live.EntityCategory.Npc && e.Poi)
            {
                rt.DrawEllipse(new Ellipse(p, r + 3, r + 3), _bLandmark!, 1.5f);
                if (rs?.ShowNpcLabels != false)
                {
                    var npcFs = rs?.LandmarkFontSize ?? 14f;
                    var npcTf = GetTextFormat(npcFs, ref _tfLandmark, ref _lastLmFs);
                    var npcName = ctx.EntityNames?.ResolveOrShorten(e.Metadata) ?? e.Metadata.Split('/')[^1];
                    rt.DrawText(npcName, npcTf, new Rect(p.X + r + 5, p.Y - npcFs / 2, p.X + 300, p.Y + npcFs), _bNpc!);
                }
            }
            else if (e.Poi && e.Category is not (Poe2Live.EntityCategory.Monster or Poe2Live.EntityCategory.Transition or Poe2Live.EntityCategory.Npc))
            {
                rt.DrawEllipse(new Ellipse(p, r + 4, r + 4), _bLandmark!, 1.5f);
                if (rs?.ShowPoiLabels != false)
                {
                    var poiFs = rs?.LandmarkFontSize ?? 14f;
                    var poiTf = GetTextFormat(poiFs, ref _tfLandmark, ref _lastLmFs);
                    var poiName = ctx.EntityNames?.ResolveOrShorten(e.Metadata) ?? e.Metadata.Split('/')[^1];
                    rt.DrawText(poiName, poiTf, new Rect(p.X + r + 6, p.Y - poiFs / 2, p.X + 300, p.Y + poiFs), _bLandmark!);
                }
            }
            else if (e.Category == Poe2Live.EntityCategory.Chest && rs?.ShowChests != false)
            {
                if (rs?.ShowChestLabels != false)
                {
                    var chFs = rs?.ChestFontSize ?? 12f;
                    var chTf = GetTextFormat(chFs, ref _tfChest, ref _lastChFs);
                    var chLabel = DetectChestType(e.Metadata);
                    if (string.IsNullOrEmpty(chLabel))
                        chLabel = e.Rarity == Poe2Live.Rarity.Unique ? "Unique" : e.Rarity == Poe2Live.Rarity.Rare ? "Rare" : "";
                    if (e.IsLocked) chLabel = "🔒 " + chLabel;
                    if (!string.IsNullOrEmpty(chLabel))
                        rt.DrawText(chLabel, chTf, new Rect(p.X + r + 3, p.Y - chFs / 2, p.X + 200, p.Y + chFs), brush);
                }
            }
        }

        if (rs?.ShowLandmarks == false) goto skipLandmarks;
        var lmFs = rs?.LandmarkFontSize ?? 12f;
        var lmTf = GetTextFormat(lmFs, ref _tfLandmark, ref _lastLmFs);
        var lmColor = rs?.LandmarkColor ?? "#f259f2";
        if (lmColor != _lastLandmarkColor && _bLandmark != null)
        {
            ParseHex(lmColor, out var lr, out var lg, out var lb);
            _bLandmark.Color = new Color4(lr / 255f, lg / 255f, lb / 255f, 1f);
            _lastLandmarkColor = lmColor;
        }
        var lmOutW = rs?.LandmarkOutlineWidth ?? 1.6f;
        ctx.LandmarkScreenPositions?.Clear();
        foreach (var lm in ctx.Landmarks)
        {
            if (hidden != null && (hidden.IsHidden(lm.Name) || hidden.IsHidden(lm.Path))) continue;
            var p = Project(new NumVec2(lm.Center.X, lm.Center.Y), player, center, scale);
            var d = rs?.LandmarkIconSize ?? 5f;
            var diamond = new[] { new NumVec2(p.X, p.Y - d), new NumVec2(p.X + d, p.Y), new NumVec2(p.X, p.Y + d), new NumVec2(p.X - d, p.Y) };
            for (var i = 0; i < 4; i++) rt.DrawLine(diamond[i], diamond[(i + 1) % 4], _bLandmark!, lmOutW);
            if (rs?.ShowLandmarkLabels != false)
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
            var pem = rs?.PathEndMarkerSize ?? 5f;
            rt.FillEllipse(new Ellipse(end, pem, pem), _bTrans!);
        }

        // Distance ring
        if (rs?.ShowDistanceRing == true)
        {
            var ringR = (rs?.DistanceRingRadius ?? 80f) * scale;
            if (_bRing == null)
                _bRing = rt.CreateSolidColorBrush(new Color4(0f, 1f, 1f, 0.5f));
            rt.DrawEllipse(new Ellipse(center, ringR, ringR), _bRing, 2f);
        }

        // Player blip on top.
        var pb = rs?.PlayerBlipSize ?? 5f;
        rt.FillEllipse(new Ellipse(center, pb, pb), _bPlayer!);
    }

    private static string DetectChestType(string meta)
    {
        if (meta.Contains("Strongbox", StringComparison.OrdinalIgnoreCase)) return "Strongbox";
        if (meta.Contains("Abyss", StringComparison.OrdinalIgnoreCase)) return "Abyss Chest";
        if (meta.Contains("Ritual", StringComparison.OrdinalIgnoreCase)) return "Ritual";
        if (meta.Contains("Expedition", StringComparison.OrdinalIgnoreCase)) return "Expedition";
        if (meta.Contains("Breach", StringComparison.OrdinalIgnoreCase)) return "Breach";
        if (meta.Contains("Delve", StringComparison.OrdinalIgnoreCase)) return "Delve";
        if (meta.Contains("Heist", StringComparison.OrdinalIgnoreCase)) return "Heist";
        if (meta.Contains("Megalith", StringComparison.OrdinalIgnoreCase)) return "Megalith";
        if (meta.Contains("Reward", StringComparison.OrdinalIgnoreCase)) return "Reward";
        if (meta.Contains("Spire", StringComparison.OrdinalIgnoreCase)) return "Spire";
        if (meta.Contains("Trap", StringComparison.OrdinalIgnoreCase)) return "Trap Chest";
        if (meta.Contains("Tutorial", StringComparison.OrdinalIgnoreCase)) return "Tutorial";
        return "";
    }

    private ID2D1SolidColorBrush GetLeagueBrush(ID2D1RenderTarget rt, Poe2Live.LeagueMechanic league)
    {
        if (_leagueBrushes.TryGetValue(league, out var b)) return b;
        var col = LeagueStyles.TryGetValue(league, out var style) ? style.Color : ColObject;
        b = rt.CreateSolidColorBrush(col);
        _leagueBrushes[league] = b;
        return b;
    }

    private void DrawInspector(ID2D1RenderTarget rt, RenderContext ctx)
    {
        if (ctx.InspectedMeta == null) return;
        var name = ctx.InspectedName ?? "";
        var meta = ctx.InspectedMeta;

        const float ix = 10, iy = 72;
        var boxW = Math.Max(name.Length, meta.Length) * 7.3f + 20;
        rt.FillRoundedRectangle(
            new RoundedRectangle(new Vortice.RawRectF(ix, iy, ix + boxW, iy + 42), 4, 4), _bPanel!);
        rt.DrawRoundedRectangle(
            new RoundedRectangle(new Vortice.RawRectF(ix, iy, ix + boxW, iy + 42), 4, 4), _bPlayer!, 1f);
        rt.DrawText(name, _tf!, new Rect(ix + 6, iy + 3, ix + boxW, iy + 18), _bText!);
        rt.DrawText(meta, _tf!, new Rect(ix + 6, iy + 21, ix + boxW, iy + 36), _bCheatOff!);
    }

    private IDWriteTextFormat? _tfPathTarget;
    private float _lastPathTargetFs;

    private void DrawPathTarget(ID2D1RenderTarget rt, RenderContext ctx)
    {
        var name = ctx.PathTargetName;
        if (string.IsNullOrEmpty(name)) return;

        var fs = ctx.Radar?.LandmarkFontSize is > 0 ? ctx.Radar.LandmarkFontSize + 6 : 20f;
        var tf = GetTextFormat(fs, ref _tfPathTarget, ref _lastPathTargetFs);

        var text = $">> {name}";
        float boxW = text.Length * (fs * 0.55f) + 24;
        float boxH = fs + 12;
        float boxX = ctx.WindowWidth * 0.5f - boxW * 0.5f;
        float boxY = 52;

        rt.FillRoundedRectangle(
            new RoundedRectangle(new Vortice.RawRectF(boxX, boxY, boxX + boxW, boxY + boxH), 6, 6),
            _bPanel!);
        rt.DrawText(text, tf,
            new Rect(boxX + 12, boxY + 4, boxX + boxW - 12, boxY + boxH),
            _bTrans!);
    }

    private IDWriteTextFormat? _tfGuideTitle, _tfGuideBody;
    private float _lastGuideTitleFs, _lastGuideBodyFs;

    private void DrawZoneGuide(ID2D1RenderTarget rt, RenderContext ctx)
    {
        var title = ctx.ZoneGuideTitle ?? "";
        var notes = ctx.ZoneGuideNotes ?? "";
        if (title.Length == 0 && notes.Length == 0) return;

        var titleFs = ctx.Radar?.ZoneGuideTitleFontSize ?? 22f;
        var bodyFs = ctx.Radar?.ZoneGuideBodyFontSize ?? 16f;
        var titleTf = GetTextFormat(titleFs, ref _tfGuideTitle, ref _lastGuideTitleFs);
        var bodyTf = GetTextFormat(bodyFs, ref _tfGuideBody, ref _lastGuideBodyFs);

        float panelW = ctx.WindowWidth * 0.35f;
        float panelX = 20;
        float panelY = ctx.WindowHeight * 0.15f;
        float padding = 16;
        float titleH = titleFs + 8;
        var lines = notes.Split('\n');
        float bodyH = lines.Length * (bodyFs + 4) + padding;
        float panelH = titleH + bodyH + padding * 2;

        rt.FillRoundedRectangle(
            new RoundedRectangle(new Vortice.RawRectF(panelX, panelY, panelX + panelW, panelY + panelH), 8, 8),
            _bPanel!);
        rt.DrawRoundedRectangle(
            new RoundedRectangle(new Vortice.RawRectF(panelX, panelY, panelX + panelW, panelY + panelH), 8, 8),
            _bLandmark!, 1.5f);

        rt.DrawText(title, titleTf!,
            new Rect(panelX + padding, panelY + padding, panelX + panelW - padding, panelY + padding + titleH),
            _bLandmark!);

        float y = panelY + padding + titleH + 8;
        foreach (var line in lines)
        {
            if (line.Length > 0)
                rt.DrawText(line, bodyTf!,
                    new Rect(panelX + padding, y, panelX + panelW - padding, y + bodyFs + 4),
                    _bText!);
            y += bodyFs + 4;
        }
    }

    private void DrawMinimap(ID2D1RenderTarget rt, RenderContext ctx)
    {
        var rs = ctx.Radar!;
        var sz = rs.MinimapSize;
        var mmScale = ctx.GameMinimap.Available
            ? ctx.GameMinimap.Zoom * (sz / 677f) * rs.MinimapScale
            : rs.MinimapScale;
        var player = ctx.PlayerGrid;

        float mx, my;
        switch (rs.MinimapPosition)
        {
            case "topleft":     mx = 10; my = 75; break;
            case "topright":    mx = ctx.WindowWidth - sz - 10; my = 75; break;
            case "bottomleft":  mx = 10; my = ctx.WindowHeight - sz - 10; break;
            default:            mx = ctx.WindowWidth - sz - 10; my = ctx.WindowHeight - sz - 10; break;
        }
        mx += rs.MinimapOffsetX;
        my += rs.MinimapOffsetY;

        var center = new NumVec2(mx + sz / 2, my + sz / 2);

        // Background
        rt.FillRoundedRectangle(
            new RoundedRectangle(new Vortice.RawRectF(mx - 2, my - 2, mx + sz + 2, my + sz + 2), 6, 6),
            _bPanel!);

        // Clip to minimap area
        rt.PushAxisAlignedClip(new Vortice.RawRectF(mx, my, mx + sz, my + sz), AntialiasMode.Aliased);

        // Terrain
        if (rs.MinimapShowTerrain && ctx.Terrain is { } t)
        {
            _terrain ??= new TerrainBitmap(rt);
            if (_terrain.Bitmap is { } bmp)
            {
                var p00 = Project(new NumVec2(0, 0), player, center, mmScale);
                var p10 = Project(new NumVec2(t.Width, 0), player, center, mmScale);
                var p01 = Project(new NumVec2(0, t.Height), player, center, mmScale);
                var ex = (p10 - p00) / t.Width;
                var ey = (p01 - p00) / t.Height;
                var prev = rt.Transform;
                rt.Transform = new Matrix3x2(ex.X, ex.Y, ey.X, ey.Y, p00.X, p00.Y);
                rt.DrawBitmap(bmp, rs.MinimapOpacity, BitmapInterpolationMode.Linear, new Rect(0, 0, t.Width, t.Height));
                rt.Transform = prev;
            }
        }

        // Entity dots
        var hideJunkMm = rs.HideJunkEntities;
        var hiddenMm = ctx.Hidden;
        var mmDotScale = rs.MinimapDotScale;
        foreach (var e in ctx.Entities)
        {
            if (hideJunkMm && JunkFilter.IsJunk(e.Metadata)) continue;
            if (hiddenMm != null && hiddenMm.IsHidden(e.Metadata)) continue;
            if (!e.IsAlive && e.HpMax > 0 && rs.ShowDeadMonsters != true) continue;
            if (!e.IsTargetable && rs.HideUntargetable) continue;
            if (e.IsFriendly && e.Category == Poe2Live.EntityCategory.Monster && !rs.ShowFriendlyEntities) continue;
            ID2D1SolidColorBrush? b; float r;
            switch (e.Category)
            {
                case Poe2Live.EntityCategory.Monster:
                    if (!rs.MinimapShowMonsters) continue;
                    if (e.IsBoss && e.IsAlive && rs.MinimapShowBosses)
                    {
                        (b, r) = (_bUnique, 5f * mmDotScale);
                        break;
                    }
                    (b, r) = e.Rarity switch
                    {
                        Poe2Live.Rarity.Unique => (_bUnique, 4f * mmDotScale),
                        Poe2Live.Rarity.Rare   => (_bRare, 3.5f * mmDotScale),
                        Poe2Live.Rarity.Magic  => (_bMagic, 2.5f * mmDotScale),
                        _ when rs.ShowNormalMonsters => (_bMonster, 1.8f * mmDotScale),
                        _ => (null, 0f),
                    };
                    break;
                case Poe2Live.EntityCategory.Npc:
                    if (!rs.MinimapShowNpcs) continue;
                    (b, r) = (_bNpc, 2.5f * mmDotScale); break;
                case Poe2Live.EntityCategory.Chest:
                    if (!rs.MinimapShowChests || e.Opened) continue;
                    if (e.Rarity is not (Poe2Live.Rarity.Rare or Poe2Live.Rarity.Unique) && !rs.ShowNormalChests) continue;
                    (b, r) = (_bChest, 2.5f * mmDotScale); break;
                case Poe2Live.EntityCategory.Transition:
                    if (!rs.MinimapShowTransitions) continue;
                    (b, r) = (_bTrans, 3f * mmDotScale); break;
                default: continue;
            }
            if (b == null) continue;
            var p = Project(new NumVec2(e.Grid.X, e.Grid.Y), player, center, mmScale);
            rt.FillEllipse(new Ellipse(p, r, r), b);

            // Minimap labels
            var mmLabelFs = rs.MinimapLabelFontSize;
            if (e.IsBoss && e.IsAlive && rs.MinimapLabelBoss)
            {
                var mmLabelTf = GetTextFormat(mmLabelFs, ref _tfTransition, ref _lastTrFs);
                var bossLabel = ctx.EntityNames?.ResolveOrShorten(e.Metadata) ?? e.Metadata.Split('/')[^1];
                rt.DrawText(bossLabel, mmLabelTf, new Rect(p.X + r + 2, p.Y - mmLabelFs / 2, p.X + 150, p.Y + mmLabelFs), _bUnique!);
            }
            else if (e.Category == Poe2Live.EntityCategory.Monster && e.IsAlive && e.Rarity == Poe2Live.Rarity.Unique && rs.MinimapLabelUnique)
            {
                var mmLabelTf = GetTextFormat(mmLabelFs, ref _tfTransition, ref _lastTrFs);
                var uLabel = ctx.EntityNames?.ResolveOrShorten(e.Metadata) ?? e.Metadata.Split('/')[^1];
                rt.DrawText(uLabel, mmLabelTf, new Rect(p.X + r + 2, p.Y - mmLabelFs / 2, p.X + 120, p.Y + mmLabelFs), _bUnique!);
            }
            else if (e.Category == Poe2Live.EntityCategory.Transition && rs.MinimapLabelTransition)
            {
                var mmLabelTf = GetTextFormat(mmLabelFs, ref _tfTransition, ref _lastTrFs);
                var trLabel = ctx.EntityNames?.ResolveOrShorten(e.Metadata) ?? e.Metadata.Split('/')[^1];
                var destArea = ctx.GameData?.GetArea(trLabel);
                if (destArea != null) trLabel = destArea.Name;
                rt.DrawText(trLabel, mmLabelTf, new Rect(p.X + r + 2, p.Y - mmLabelFs / 2, p.X + 120, p.Y + mmLabelFs), _bTrans!);
            }
            else if (e.Category == Poe2Live.EntityCategory.Npc && e.Poi && rs.MinimapLabelNpc)
            {
                var mmLabelTf = GetTextFormat(mmLabelFs, ref _tfTransition, ref _lastTrFs);
                var npcLabel = ctx.EntityNames?.ResolveOrShorten(e.Metadata) ?? e.Metadata.Split('/')[^1];
                rt.DrawText(npcLabel, mmLabelTf, new Rect(p.X + r + 2, p.Y - mmLabelFs / 2, p.X + 120, p.Y + mmLabelFs), _bNpc!);
            }
        }

        // Watched entity labels on minimap
        if (rs.MinimapLabelWatched && ctx.Watched != null)
        {
            var mmLabelFs = rs.MinimapLabelFontSize;
            var mmLabelTf = GetTextFormat(mmLabelFs, ref _tfTransition, ref _lastTrFs);
            foreach (var e in ctx.Entities)
            {
                if (!ctx.Watched.IsWatched(e.Metadata)) continue;
                var w = ctx.Watched.Match(e.Metadata);
                if (w == null || !w.Enabled) continue;
                var p = Project(new NumVec2(e.Grid.X, e.Grid.Y), player, center, mmScale);
                rt.DrawText(w.Label, mmLabelTf, new Rect(p.X + 4, p.Y - mmLabelFs / 2, p.X + 120, p.Y + mmLabelFs), _bText!);
            }
        }

        // Player blip
        var mpb = rs?.MinimapPlayerBlipSize ?? 4f;
        rt.FillEllipse(new Ellipse(center, mpb, mpb), _bPlayer!);

        // Path line
        if (rs!.MinimapShowPath && ctx.PathPoints is { Count: >= 2 } pp)
        {
            for (var i = 0; i < pp.Count - 1; i++)
            {
                var a = Project(new NumVec2(pp[i].X, pp[i].Y), player, center, mmScale);
                var b2 = Project(new NumVec2(pp[i + 1].X, pp[i + 1].Y), player, center, mmScale);
                rt.DrawLine(a, b2, _bTrans!, 1.5f);
            }
        }

        rt.PopAxisAlignedClip();

        // Border
        rt.DrawRoundedRectangle(
            new RoundedRectangle(new Vortice.RawRectF(mx - 2, my - 2, mx + sz + 2, my + sz + 2), 6, 6),
            _bText!, 1f);
    }

    private static void ParseHex(string hex, out byte r, out byte g, out byte b)
    {
        hex = hex.TrimStart('#');
        if (hex.Length >= 6)
        {
            r = Convert.ToByte(hex[..2], 16);
            g = Convert.ToByte(hex[2..4], 16);
            b = Convert.ToByte(hex[4..6], 16);
        }
        else { r = 60; g = 220; b = 255; }
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
        _bCheatOn?.Dispose(); _bCheatOff?.Dispose(); _bCheatMiss?.Dispose(); _bFog?.Dispose(); _bRing?.Dispose(); _bOutline?.Dispose(); _bFriendly?.Dispose();
        _bStyle?.Dispose();
        foreach (var b in _leagueBrushes.Values) b?.Dispose(); _leagueBrushes.Clear();
        _geoTriangle?.Dispose(); _geoStar?.Dispose(); _geoDiamond?.Dispose(); _geoPlus?.Dispose();
        var disposed = new HashSet<nint>();
        foreach (var g in _geoCache.Values) { if (g != null && disposed.Add(g.NativePointer)) g.Dispose(); }
        _geoCache.Clear();
        _tf?.Dispose(); _tfLandmark?.Dispose(); _tfTransition?.Dispose(); _tfChest?.Dispose(); _tfStatus?.Dispose();
        _terrain?.Dispose();
    }
}
