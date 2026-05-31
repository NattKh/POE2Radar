namespace POE2Radar.Core.Pathfinding;

public sealed class ExplorationTracker
{
    private HashSet<long> _visited = new();
    private nint _areaKey;
    private const int RevealRadius = 12;

    public bool IsExplored(int x, int y) => _visited.Contains(Key(x, y));

    public int ExploredCount => _visited.Count;

    public void Update(float playerGridX, float playerGridY, nint areaInstance)
    {
        if (areaInstance != _areaKey) { _visited = new(); _areaKey = areaInstance; }

        var cx = (int)playerGridX;
        var cy = (int)playerGridY;
        for (var dy = -RevealRadius; dy <= RevealRadius; dy++)
            for (var dx = -RevealRadius; dx <= RevealRadius; dx++)
                if (dx * dx + dy * dy <= RevealRadius * RevealRadius)
                    _visited.Add(Key(cx + dx, cy + dy));
    }

    private static long Key(int x, int y) => ((long)y << 32) | (uint)x;
}
