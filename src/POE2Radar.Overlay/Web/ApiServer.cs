using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using POE2Radar.Core.Game;

namespace POE2Radar.Overlay.Web;

public sealed class ApiServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Func<RadarState> _state;
    private readonly WatchedEntities _watched;
    private readonly RadarSettings _settings;
    private volatile bool _running;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public WatchedEntities Watched => _watched;

    public ApiServer(Func<RadarState> state, WatchedEntities watched, RadarSettings settings, int port = 7777)
    {
        _state = state;
        _watched = watched;
        _settings = settings;
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public void Start()
    {
        _listener.Start();
        _running = true;
        new Thread(Loop) { IsBackground = true, Name = "ApiThread" }.Start();
    }

    private void Loop()
    {
        while (_running)
        {
            HttpListenerContext ctx;
            try { ctx = _listener.GetContext(); }
            catch { return; }
            try { Handle(ctx); }
            catch (Exception ex) { TryWrite(ctx, 500, "application/json", JsonSerializer.Serialize(new { error = ex.Message }, Json)); }
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url?.AbsolutePath ?? "/";
        var method = ctx.Request.HttpMethod;
        var q = ctx.Request.QueryString;
        var s = _state();

        switch (path)
        {
            case "/":
                WriteHtml(ctx, DashboardHtml.Page);
                break;

            case "/health":
                WriteJson(ctx, new { ok = true, inGame = s.InGame });
                break;

            case "/state":
            {
                var counts = s.Entities.GroupBy(e => e.Category).ToDictionary(g => g.Key.ToString(), g => g.Count());
                WriteJson(ctx, new
                {
                    s.InGame, areaCode = s.AreaCode, areaHash = s.AreaHash, areaLevel = s.AreaLevel,
                    mapVisible = s.MapVisible, zoom = s.Zoom,
                    hpPct = s.HpPct, manaPct = s.ManaPct, autoFlask = s.AutoFlask, flask = s.FlaskNote,
                    player = new { x = s.Player.X, y = s.Player.Y },
                    entityCount = s.Entities.Count, counts,
                });
                break;
            }

            case "/landmarks":
            {
                var list = s.Landmarks.OrderBy(l => Dist(l.Center, s.Player)).Select(l => new
                {
                    name = l.Name, path = l.Path, tiles = l.TileCount,
                    x = l.Center.X, y = l.Center.Y, dist = (int)Dist(l.Center, s.Player),
                });
                WriteJson(ctx, list);
                break;
            }

            case "/entities":
            {
                var category = q["category"];
                var aliveOnly = string.Equals(q["alive"], "true", StringComparison.OrdinalIgnoreCase);
                _ = float.TryParse(q["radius"], out var radius);
                _ = int.TryParse(q["limit"], out var limit);
                if (limit <= 0) limit = 500;

                IEnumerable<Poe2Live.EntityDot> q2 = s.Entities;
                if (!string.IsNullOrEmpty(category))
                    q2 = q2.Where(e => string.Equals(e.Category.ToString(), category, StringComparison.OrdinalIgnoreCase));
                if (aliveOnly) q2 = q2.Where(e => e.HpCur > 0);
                if (radius > 0) q2 = q2.Where(e => Dist(e.Grid, s.Player) <= radius);

                var list = q2.OrderBy(e => Dist(e.Grid, s.Player)).Take(limit).Select(e => new
                {
                    id = e.Id, category = e.Category.ToString(), metadata = e.Metadata,
                    poi = e.Poi, friendly = e.IsFriendly, rarity = e.Rarity.ToString(),
                    x = e.Grid.X, y = e.Grid.Y, hpCur = e.HpCur, hpMax = e.HpMax,
                    alive = e.IsAlive, dist = (int)Dist(e.Grid, s.Player),
                    watched = _watched.IsWatched(e.Metadata),
                });
                WriteJson(ctx, list);
                break;
            }

            case "/api/watched":
            {
                if (method == "GET")
                {
                    WriteJson(ctx, _watched.All.Values.ToList());
                }
                else if (method == "POST")
                {
                    var body = ReadBody(ctx);
                    var entry = JsonSerializer.Deserialize<WatchedEntry>(body, Json);
                    if (entry != null)
                    {
                        _watched.Add(entry.Pattern, entry.Label, entry.Color);
                        WriteJson(ctx, new { ok = true });
                    }
                    else WriteJson(ctx, new { error = "bad json" }, 400);
                }
                else if (method == "DELETE")
                {
                    var pattern = q["pattern"];
                    if (!string.IsNullOrEmpty(pattern))
                    {
                        _watched.Remove(pattern);
                        WriteJson(ctx, new { ok = true });
                    }
                    else WriteJson(ctx, new { error = "missing pattern" }, 400);
                }
                break;
            }

            case "/api/settings":
            {
                if (method == "GET")
                    WriteJson(ctx, _settings);
                else if (method == "POST")
                {
                    var body = ReadBody(ctx);
                    var patch = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
                    if (patch != null) ApplySettings(patch);
                    WriteJson(ctx, _settings);
                }
                break;
            }

            case "/api/database":
            {
                WriteJson(ctx, LoadEntityDatabase());
                break;
            }

            default:
                WriteJson(ctx, new { error = "not found", path }, 404);
                break;
        }
    }

    private void ApplySettings(Dictionary<string, JsonElement> patch)
    {
        foreach (var (key, val) in patch)
        {
            var prop = typeof(RadarSettings).GetProperties()
                .FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
            if (prop == null) continue;
            try
            {
                if (prop.PropertyType == typeof(float))
                    prop.SetValue(_settings, val.GetSingle());
                else if (prop.PropertyType == typeof(bool))
                    prop.SetValue(_settings, val.GetBoolean());
                else if (prop.PropertyType == typeof(string))
                    prop.SetValue(_settings, val.GetString());
                else if (prop.PropertyType == typeof(int))
                    prop.SetValue(_settings, val.GetInt32());
            }
            catch { }
        }
        _settings.Save();
    }

    private static string[]? _dbCache;
    private static string[] LoadEntityDatabase()
    {
        if (_dbCache != null) return _dbCache;
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.Contains("entity_database"));
            if (name == null) return _dbCache = [];
            using var stream = asm.GetManifestResourceStream(name)!;
            _dbCache = JsonSerializer.Deserialize<string[]>(stream) ?? [];
        }
        catch { _dbCache = []; }
        return _dbCache;
    }

    private static float Dist(System.Numerics.Vector2 a, System.Numerics.Vector2 b) => (a - b).Length();

    private static string ReadBody(HttpListenerContext ctx)
    {
        using var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
        return reader.ReadToEnd();
    }

    private static void WriteJson(HttpListenerContext ctx, object data, int status = 200)
        => TryWrite(ctx, status, "application/json", JsonSerializer.Serialize(data, Json));

    private static void WriteHtml(HttpListenerContext ctx, string html)
        => TryWrite(ctx, 200, "text/html; charset=utf-8", html);

    private static void TryWrite(HttpListenerContext ctx, int status, string contentType, string body)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = contentType;
            ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.OutputStream.Close();
        }
        catch { }
    }

    public void Dispose()
    {
        _running = false;
        try { _listener.Stop(); } catch { }
        _listener.Close();
    }
}

public sealed record RadarState(
    bool InGame, uint AreaHash, int AreaLevel, bool MapVisible, float Zoom,
    System.Numerics.Vector2 Player,
    IReadOnlyList<Poe2Live.EntityDot> Entities,
    IReadOnlyList<Poe2Live.Landmark> Landmarks,
    float HpPct, float ManaPct, bool AutoFlask, string FlaskNote,
    string AreaCode, string CharName, int CharLevel)
{
    public static readonly RadarState Empty =
        new(false, 0, 0, false, 0, System.Numerics.Vector2.Zero,
            Array.Empty<Poe2Live.EntityDot>(), Array.Empty<Poe2Live.Landmark>(), 100, 100, false, "", "", "", 0);
}
