namespace POE2Radar.Core.Game;

public static class JunkFilter
{
    private static readonly string[] JunkPatterns =
    [
        "/attachments",
        "monstermods",
        "microtransactions",
        "/timelines/",
        "stashskins",
        "/fx/",
        "/mat/",
        "/ao/",
        "/epk/",
        "/graph/",
        "/audio/",
        "/pet/",
        "/clone/",
        "playersummoned",
        "essencemoddaemons",
        "tormentedspirits",
        "/daemon/",
        "bossroomminimapicon",
        "/environment/",
        "hairstyles",
        "/outfits/",
        "/runemarked",
    ];

    public static bool IsJunk(string metadata)
    {
        foreach (var p in JunkPatterns)
            if (metadata.Contains(p, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
