using System.Reflection;
using System.Text.Json;

namespace POE2Radar.Core.Game;

public static class CustomLandmarkData
{
    private static Dictionary<string, Dictionary<string, string>>? _data;

    public static IReadOnlyDictionary<string, Dictionary<string, string>> Load()
    {
        if (_data != null) return _data;

        _data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var resName = asm.GetManifestResourceNames().FirstOrDefault(n => n.Contains("CustomLandmarks"));
            if (resName == null) return _data;

            using var stream = asm.GetManifestResourceStream(resName);
            if (stream == null) return _data;

            var doc = JsonDocument.Parse(stream);
            foreach (var areaProp in doc.RootElement.EnumerateObject())
            {
                var areaCode = areaProp.Name;
                var tiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var tileProp in areaProp.Value.EnumerateObject())
                {
                    var tgtPath = tileProp.Name;
                    var colonIdx = tgtPath.IndexOf(':');
                    var cleanPath = colonIdx >= 0 ? tgtPath[..colonIdx] : tgtPath;
                    cleanPath = cleanPath.Replace(".tdtx", ".tdt");
                    tiles[cleanPath] = tileProp.Value.GetString() ?? tileProp.Name;
                }
                _data[areaCode] = tiles;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load custom landmarks: {ex.Message}");
        }
        return _data;
    }

    public static string? TryMatch(string areaCode, string tilePath)
    {
        var data = Load();

        if (data.TryGetValue(areaCode, out var areaMap))
        {
            foreach (var (pattern, label) in areaMap)
                if (tilePath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return label;
        }

        if (data.TryGetValue("*", out var globalMap))
        {
            foreach (var (pattern, label) in globalMap)
                if (tilePath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return label;
        }

        return null;
    }
}
