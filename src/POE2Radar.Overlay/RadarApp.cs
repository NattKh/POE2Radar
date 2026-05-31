using System.Runtime.InteropServices;
using NumVec2 = System.Numerics.Vector2;
using POE2Radar.Core;
using POE2Radar.Core.Cheats;
using POE2Radar.Core.Game;
using POE2Radar.Core.Pathfinding;
using POE2Radar.Overlay.Input;
using POE2Radar.Overlay.Native;
using POE2Radar.Overlay.Web;

#pragma warning disable CA1416

namespace POE2Radar.Overlay;

public sealed class RadarApp : IDisposable
{
    private const int TargetHz = 144;
    private const int WorldHz = 30;

    private readonly ProcessHandle _process;
    private readonly MemoryReader _reader;
    private readonly Poe2Live _live;
    private readonly CheatManager _cheats;
    private readonly OverlayWindow _window;
    private readonly OverlayRenderer _renderer;
    private readonly WatchedEntities _watched;
    private readonly PathingTargets _pathing;
    private readonly ApiServer _api;
    private readonly RadarSettings _radarSettings;
    private SettingsForm? _settingsForm;
    private volatile RadarState _state = RadarState.Empty;

    private DateTime _worldAt = DateTime.MinValue;
    private List<Poe2Live.EntityDot> _entities = new();
    private IReadOnlyList<Poe2Live.Landmark> _landmarks = Array.Empty<Poe2Live.Landmark>();
    private Poe2Live.TerrainData? _terrain;
    private uint _areaHash;
    private nint _lastAreaInstance;
    private nint _gameHwnd;
    private volatile bool _shutdown;

    private DateTime _nextKeyAt = DateTime.MinValue;
    private List<(int X, int Y)>? _pathPoints;
    private NumVec2 _lastPathPlayerGrid;
    private string _lastPathTarget = "";
    private string? _manualPathPattern;
    private readonly List<(float ScreenX, float ScreenY, string Metadata)> _entityScreenPos = new();

    private const int LifeVk = 0x31, ManaVk = 0x32;
    private static readonly TimeSpan LifeCooldown = TimeSpan.FromMilliseconds(2500);
    private static readonly TimeSpan ManaCooldown = TimeSpan.FromMilliseconds(2000);
    private bool _autoFlask = true;
    private DateTime _lifeFiredAt = DateTime.MinValue, _manaFiredAt = DateTime.MinValue;
    private DateTime _nextToggleAt = DateTime.MinValue;
    private float _hpPct = 100f, _manaPct = 100f;
    private string _flaskNote = "";
    private string _areaCode = "", _charName = "";
    private int _charLevel;
    private float[]? _cameraMatrix;
    private bool _overlayVisible = true;

    private DateTime _nextCheatKeyAt = DateTime.MinValue;
    private static readonly (int Vk, string Name)[] CheatKeys =
    [
        (0x70, "NoAtlasFog"),        // F1
        (0x71, "RevealMap"),         // F2
        (0x72, "InfiniteZoom"),      // F3
        (0x73, "EnemyHealthBars"),   // F4
        (0x74, "PlayerLightRadius"), // F5
    ];

    public void RequestShutdown() => _shutdown = true;

    public RadarApp(ProcessHandle process, MemoryReader reader, nint gameStateSlot)
    {
        _process = process;
        _reader = reader;
        _live = new Poe2Live(reader, gameStateSlot);
        _cheats = new CheatManager(process, reader);
        Console.WriteLine("\nScanning cheat patterns...");
        _cheats.ScanAndResolve();
        Console.WriteLine("Hotkeys: F1-F5 cheats, F8 flask, F9 settings, F10 overlay, F11 web dashboard\n");
        _window = OverlayWindow.Create();
        _renderer = new OverlayRenderer(_window);
        var configDir = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? ".", "config");
        _radarSettings = RadarSettings.Load(Path.Combine(configDir, "radar_settings.json"));
        _watched = new WatchedEntities(Path.Combine(configDir, "watched_entities.json"));
        _pathing = new PathingTargets(Path.Combine(configDir, "pathing_targets.json"));
        _api = new ApiServer(() => _state, _watched, _radarSettings, _pathing);
        try { _api.Start(); Console.WriteLine("API on http://localhost:7777 (/state, /entities)"); }
        catch (Exception ex) { Console.Error.WriteLine($"API server disabled: {ex.Message}"); }
    }

    public void Run()
    {
        var targetMs = 1000 / TargetHz;
        _gameHwnd = OverlayNative.FindWindowForProcess(_process.ProcessId);
        while (!_shutdown)
        {
            if (_gameHwnd == 0) _gameHwnd = OverlayNative.FindWindowForProcess(_process.ProcessId);
            if (_gameHwnd != 0) _window.TrackGameWindow(_gameHwnd);
            if (!_window.PumpMessages()) break;
            Tick();
            Thread.Sleep(targetMs);
        }
    }

    private void Tick()
    {
        HandleCalibrationKeys();
        HandleCheatKeys();
        HandleSettingsToggle();
        HandleAltClick();

        var inGame = _live.TryResolve(out var inGameState, out var areaInstance, out var localPlayer);
        var player = NumVec2.Zero;
        var map = default(Poe2Live.MapUi);
        var areaLevel = 0;

        if (inGame)
        {
            if (areaInstance != _lastAreaInstance) { _terrain = null; _lastAreaInstance = areaInstance; }
            _areaHash = _live.AreaHash(areaInstance);
            areaLevel = _live.AreaLevel(areaInstance);

            player = _live.PlayerGrid(localPlayer) ?? NumVec2.Zero;
            map = _live.ReadMap(inGameState, areaInstance);
            _areaCode = _live.AreaCode(areaInstance);
            _charName = _live.PlayerName(localPlayer);
            _charLevel = _live.PlayerLevel(localPlayer);
            _cameraMatrix = _live.CameraMatrix(inGameState);
            TickAutoFlask(localPlayer);

            var now = DateTime.UtcNow;
            if ((now - _worldAt).TotalMilliseconds >= 1000.0 / WorldHz)
            {
                _worldAt = now;
                _terrain ??= _live.Terrain(areaInstance);
                _entities = _live.Entities(areaInstance);
                _landmarks = _live.Landmarks(areaInstance);
                UpdatePath(player);
            }
        }

        _state = new RadarState(inGame, _areaHash, areaLevel, map.IsVisible, map.Zoom, player, _entities, _landmarks,
            _hpPct, _manaPct, _autoFlask, _flaskNote, _areaCode, _charName, _charLevel);

        var ctx = new RenderContext(
            InGame: inGame,
            Active: _gameHwnd != 0 && GetForegroundWindow() == _gameHwnd,
            WindowWidth: _window.Width,
            WindowHeight: _window.Height,
            PlayerGrid: player,
            Map: map,
            Entities: _entities,
            Landmarks: _landmarks,
            AreaHash: _areaHash,
            Terrain: _terrain,
            ScaleMul: _radarSettings.ScaleMul,
            OffsetX: _radarSettings.OffsetX,
            OffsetY: _radarSettings.OffsetY,
            HpPct: _hpPct,
            ManaPct: _manaPct,
            FlaskNote: _flaskNote,
            AreaCode: _areaCode,
            CharLevel: _charLevel,
            CameraMatrix: _cameraMatrix,
            CheatStatus: _cheats.GetStatus(),
            Radar: _radarSettings,
            OverlayVisible: _overlayVisible,
            Watched: _watched,
            PathPoints: _pathPoints,
            EntityScreenPositions: _entityScreenPos);
        _renderer.Render(ctx);
    }

    private void HandleSettingsToggle()
    {
        // F7 cycle pathing target
        if (Down(0x76) && DateTime.UtcNow >= _nextToggleAt)
        {
            _nextToggleAt = DateTime.UtcNow.AddMilliseconds(300);
            var next = _pathing.CycleNext();
            if (next != null)
            {
                _manualPathPattern = next.Pattern;
                _pathPoints = null;
                _lastPathTarget = "";
                Console.WriteLine($"\nPath target: {next.Label} ({next.Pattern})");
            }
        }
        // F11 open web dashboard
        if (Down(0x7A) && DateTime.UtcNow >= _nextToggleAt)
        {
            _nextToggleAt = DateTime.UtcNow.AddMilliseconds(500);
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("http://localhost:7777") { UseShellExecute = true }); }
            catch { }
        }
        // F10 toggle overlay visibility
        if (Down(0x79) && DateTime.UtcNow >= _nextToggleAt)
        {
            _overlayVisible = !_overlayVisible;
            _nextToggleAt = DateTime.UtcNow.AddMilliseconds(300);
            Console.WriteLine($"\nOverlay: {(_overlayVisible ? "VISIBLE" : "HIDDEN")}");
        }
        if (Down(0x78) && DateTime.UtcNow >= _nextToggleAt) // F9
        {
            _nextToggleAt = DateTime.UtcNow.AddMilliseconds(300);
            if (_settingsForm == null || _settingsForm.IsDisposed)
            {
                _settingsForm = new SettingsForm(_cheats, _radarSettings);
                _settingsForm.Show();
            }
            else if (_settingsForm.Visible)
            {
                _settingsForm.Hide();
            }
            else
            {
                _settingsForm.SyncState();
                _settingsForm.Show();
            }
        }
    }

    private void TickAutoFlask(nint localPlayer)
    {
        if (_live.PlayerVitals(localPlayer) is not { } v) return;
        _hpPct = v.HpPct; _manaPct = v.ManaPct;

        if (!_autoFlask) { _flaskNote = "OFF (F8)"; return; }
        if (GetForegroundWindow() != _gameHwnd) { _flaskNote = "paused (PoE2 not focused)"; return; }
        _flaskNote = "armed";

        var hpThresh = _radarSettings.HpThreshold;
        var manaThresh = _radarSettings.ManaThreshold;

        var now = DateTime.UtcNow;
        if (v.HpPct < hpThresh && now - _lifeFiredAt >= LifeCooldown)
        {
            SendInputNative.Tap(LifeVk); _lifeFiredAt = now; _flaskNote = $"life@{v.HpPct:F0}%";
        }
        if (v.ManaPct < manaThresh && now - _manaFiredAt >= ManaCooldown)
        {
            SendInputNative.Tap(ManaVk); _manaFiredAt = now; _flaskNote = $"mana@{v.ManaPct:F0}%";
        }
    }

    private void HandleAltClick()
    {
        var altHeld = Down(0x12); // VK_MENU (Alt)
        _window.SetInteractive(altHeld);

        if (!altHeld || !_window.HasClick) return;
        _window.ConsumeClick();

        var cx = _window.ClickX;
        var cy = _window.ClickY;

        // Find nearest entity to click position
        string? bestMeta = null;
        var bestDist = 20f * 20f; // 20px radius squared
        foreach (var (sx, sy, meta) in _entityScreenPos)
        {
            var dx = sx - cx;
            var dy = sy - cy;
            var d2 = dx * dx + dy * dy;
            if (d2 < bestDist) { bestDist = d2; bestMeta = meta; }
        }

        if (bestMeta != null)
        {
            var shortName = bestMeta.Split('/')[^1].Split('@')[0];
            _manualPathPattern = shortName;
            _pathPoints = null;
            _lastPathTarget = "";
            Console.WriteLine($"\nAlt+click nav: {shortName}");
        }
    }

    private void UpdatePath(NumVec2 playerGrid)
    {
        if (!_radarSettings.ShowPath || _terrain == null || _pathing.All.Count == 0)
        {
            _pathPoints = null;
            return;
        }

        string? targetPattern;
        if (_manualPathPattern != null)
        {
            targetPattern = _manualPathPattern;
        }
        else
        {
            var entityInfo = _entities
                .Select(e => (e.Metadata, Distance: (e.Grid - playerGrid).Length(), e.IsAlive))
                .ToList();
            targetPattern = _pathing.FindNearestPattern(entityInfo!);
        }

        if (targetPattern == null) { _pathPoints = null; return; }

        var playerMoved = (playerGrid - _lastPathPlayerGrid).Length() > 3f;
        var targetChanged = targetPattern != _lastPathTarget;
        if (!playerMoved && !targetChanged && _pathPoints != null) return;

        _lastPathPlayerGrid = playerGrid;
        _lastPathTarget = targetPattern;

        Poe2Live.EntityDot? closest = null;
        var closestDist = float.MaxValue;
        foreach (var e in _entities)
        {
            if (!e.IsAlive && e.HpMax > 0) continue;
            if (!e.Metadata.Contains(targetPattern, StringComparison.OrdinalIgnoreCase)) continue;
            var d = (e.Grid - playerGrid).Length();
            if (d < closestDist) { closestDist = d; closest = e; }
        }

        if (closest == null) { _pathPoints = null; return; }

        var t = _terrain;
        var result = AStarPathfinder.FindPath(t.Walkable, t.Width, t.Height,
            (int)playerGrid.X, (int)playerGrid.Y,
            (int)closest.Value.Grid.X, (int)closest.Value.Grid.Y);
        _pathPoints = result != null ? AStarPathfinder.Simplify(result.Value.Points) : null;
    }

    private void HandleCalibrationKeys()
    {
        if (Down(0x77) && DateTime.UtcNow >= _nextToggleAt)
        {
            _autoFlask = !_autoFlask;
            _nextToggleAt = DateTime.UtcNow.AddMilliseconds(300);
            Console.WriteLine($"\nAuto-flask: {(_autoFlask ? "ON" : "OFF")}");
        }
        if (DateTime.UtcNow < _nextKeyAt) return;
        var changed = true;
        if (Down(0x21)) _radarSettings.ScaleMul *= 1.03f;
        else if (Down(0x22)) _radarSettings.ScaleMul /= 1.03f;
        else if (Down(0x25)) _radarSettings.OffsetX -= 4;
        else if (Down(0x27)) _radarSettings.OffsetX += 4;
        else if (Down(0x26)) _radarSettings.OffsetY -= 4;
        else if (Down(0x28)) _radarSettings.OffsetY += 4;
        else if (Down(0x24)) { _radarSettings.ScaleMul = 1f; _radarSettings.OffsetX = 0; _radarSettings.OffsetY = 0; }
        else changed = false;
        if (changed)
        {
            _nextKeyAt = DateTime.UtcNow.AddMilliseconds(40);
            Console.Write($"\rcalib: scaleMul={_radarSettings.ScaleMul:F3} off=({_radarSettings.OffsetX:F0},{_radarSettings.OffsetY:F0})        ");
        }
    }

    private void HandleCheatKeys()
    {
        if (DateTime.UtcNow < _nextCheatKeyAt) return;
        foreach (var (vk, name) in CheatKeys)
        {
            if (!Down(vk)) continue;
            _cheats.Toggle(name);
            _nextCheatKeyAt = DateTime.UtcNow.AddMilliseconds(300);
            break;
        }
    }

    private static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    public void Dispose()
    {
        _cheats.RestoreAll();
        _settingsForm?.Dispose();
        _api.Dispose();
        _renderer.Dispose();
        _window.Dispose();
    }
}
