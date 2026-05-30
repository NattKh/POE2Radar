using System.Runtime.InteropServices;
using NumVec2 = System.Numerics.Vector2;
using POE2Radar.Core;
using POE2Radar.Core.Game;
using POE2Radar.Overlay.Input;
using POE2Radar.Overlay.Native;
using POE2Radar.Overlay.Web;

namespace POE2Radar.Overlay;

/// <summary>
/// Drives the PoE2 radar: per-tick resolve chain → read player/entities/terrain/map → render.
/// Read-only. Render rate ~144 Hz (player blip tracks live); the heavier entity/terrain walk
/// runs at ~30 Hz. Projection scale/offset are tweakable live via hotkeys for calibration.
/// </summary>
public sealed class RadarApp : IDisposable
{
    private const int TargetHz = 144;
    private const int WorldHz = 30;

    private readonly ProcessHandle _process;
    private readonly MemoryReader _reader;
    private readonly Poe2Live _live;
    private readonly OverlayWindow _window;
    private readonly OverlayRenderer _renderer;
    private readonly ApiServer _api;
    private volatile RadarState _state = RadarState.Empty;

    private DateTime _worldAt = DateTime.MinValue;
    private List<Poe2Live.EntityDot> _entities = new();
    private IReadOnlyList<Poe2Live.Landmark> _landmarks = Array.Empty<Poe2Live.Landmark>();
    private Poe2Live.TerrainData? _terrain;
    private uint _areaHash;
    private nint _lastAreaInstance;
    private nint _gameHwnd;
    private volatile bool _shutdown;

    // Live projection calibration (PageUp/Down = scale, arrows = offset, Home = reset).
    private float _scaleMul = 1.0f;
    private float _offX, _offY;
    private DateTime _nextKeyAt = DateTime.MinValue;

    // ── Auto-flask (opt-in input). Foreground + in-game gated; F8 master kill-switch. ──
    private const int   LifeVk = 0x31, ManaVk = 0x32;     // '1' = life flask, '2' = mana flask
    private const float LifeThresholdPct = 65f, ManaThresholdPct = 30f;
    private static readonly TimeSpan LifeCooldown = TimeSpan.FromMilliseconds(2500);
    private static readonly TimeSpan ManaCooldown = TimeSpan.FromMilliseconds(2000);
    private bool _autoFlask = true;                        // auto-on; toggle with F8
    private DateTime _lifeFiredAt = DateTime.MinValue, _manaFiredAt = DateTime.MinValue;
    private DateTime _nextToggleAt = DateTime.MinValue;
    private float _hpPct = 100f, _manaPct = 100f;
    private string _flaskNote = "";
    private string _areaCode = "", _charName = "";
    private int _charLevel;
    private float[]? _cameraMatrix;

    public void RequestShutdown() => _shutdown = true;

    public RadarApp(ProcessHandle process, MemoryReader reader, nint gameStateSlot)
    {
        _process = process;
        _reader = reader;
        _live = new Poe2Live(reader, gameStateSlot);
        _window = OverlayWindow.Create();
        _renderer = new OverlayRenderer(_window);
        _api = new ApiServer(() => _state);
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

        var inGame = _live.TryResolve(out var inGameState, out var areaInstance, out var localPlayer);
        var player = NumVec2.Zero;
        var map = default(Poe2Live.MapUi);
        var areaLevel = 0;

        if (inGame)
        {
            // AreaInstance is a fresh object per area — use its address to invalidate per-area caches.
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
                _landmarks = _live.Landmarks(areaInstance); // cached per area in Poe2Live
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
            ScaleMul: _scaleMul,
            OffsetX: _offX,
            OffsetY: _offY,
            HpPct: _hpPct,
            ManaPct: _manaPct,
            FlaskNote: _flaskNote,
            AreaCode: _areaCode,
            CharLevel: _charLevel,
            CameraMatrix: _cameraMatrix);
        _renderer.Render(ctx);
    }

    /// <summary>
    /// Auto-flask: press the life/mana flask key when the corresponding pool drops below its
    /// threshold. Hard-gated: enabled + PoE2 is the foreground window + per-flask cooldown.
    /// </summary>
    private void TickAutoFlask(nint localPlayer)
    {
        if (_live.PlayerVitals(localPlayer) is not { } v) return;
        _hpPct = v.HpPct; _manaPct = v.ManaPct;

        if (!_autoFlask) { _flaskNote = "OFF (F8)"; return; }
        if (GetForegroundWindow() != _gameHwnd) { _flaskNote = "paused (PoE2 not focused)"; return; }
        _flaskNote = "armed";

        var now = DateTime.UtcNow;
        if (v.HpPct < LifeThresholdPct && now - _lifeFiredAt >= LifeCooldown)
        {
            SendInputNative.Tap(LifeVk); _lifeFiredAt = now; _flaskNote = $"life@{v.HpPct:F0}%";
        }
        if (v.ManaPct < ManaThresholdPct && now - _manaFiredAt >= ManaCooldown)
        {
            SendInputNative.Tap(ManaVk); _manaFiredAt = now; _flaskNote = $"mana@{v.ManaPct:F0}%";
        }
    }

    /// <summary>Live projection calibration so the overlay can be aligned without rebuilds.</summary>
    private void HandleCalibrationKeys()
    {
        // F8 master kill-switch for auto-flask (debounced).
        if (Down(0x77) && DateTime.UtcNow >= _nextToggleAt)
        {
            _autoFlask = !_autoFlask;
            _nextToggleAt = DateTime.UtcNow.AddMilliseconds(300);
            Console.WriteLine($"\nAuto-flask: {(_autoFlask ? "ON" : "OFF")}");
        }
        if (DateTime.UtcNow < _nextKeyAt) return;
        var changed = true;
        if (Down(0x21)) _scaleMul *= 1.03f;            // PageUp
        else if (Down(0x22)) _scaleMul /= 1.03f;       // PageDown
        else if (Down(0x25)) _offX -= 4;               // Left
        else if (Down(0x27)) _offX += 4;               // Right
        else if (Down(0x26)) _offY -= 4;               // Up
        else if (Down(0x28)) _offY += 4;               // Down
        else if (Down(0x24)) { _scaleMul = 1f; _offX = _offY = 0; } // Home
        else changed = false;
        if (changed)
        {
            _nextKeyAt = DateTime.UtcNow.AddMilliseconds(40);
            Console.Write($"\rcalib: scaleMul={_scaleMul:F3} off=({_offX:F0},{_offY:F0})        ");
        }
    }

    private static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    public void Dispose()
    {
        _api.Dispose();
        _renderer.Dispose();
        _window.Dispose();
    }
}
