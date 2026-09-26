namespace PhotoIngest;

public static class Sidecars
{
    static readonly HashSet<string> SidecarOnly = new(StringComparer.OrdinalIgnoreCase) { "pp3", "thm", "xmp", "mp", "aae", "lrv" };
    static readonly string[] PrimaryRank = { "jpg", "jpeg", "heic", "heif", "mp4", "mov", "3gp", "avi", "mkv", "webm", "m4v", "orf", "nef", "arw", "cr2", "raf", "dng", "png", "tif", "tiff", "gif", "webp", "bmp", "xcf" };

    public static string Stem(string name)
    {
        var i = name.IndexOf('.');
        return i < 0 ? name : name[..i];
    }

    static int Rank(string ext)
    {
        var i = Array.IndexOf(PrimaryRank, ext.ToLowerInvariant());
        return i < 0 ? int.MaxValue : i;
    }

    static bool IsJpgLike(Item i) => Rank(i.Ext) <= 3;

    public static List<(Item primary, List<Item> sidecars)> Group(IEnumerable<Item> itemsInDir)
    {
        var result = new List<(Item, List<Item>)>();
        foreach (var g in itemsInDir.GroupBy(i => i.Stem, StringComparer.OrdinalIgnoreCase))
        {
            var members = g.OrderBy(i => Rank(i.Ext)).ThenBy(i => i.Rel, StringComparer.Ordinal).ToList();
            var sidecars = members.Where(i => SidecarOnly.Contains(i.Ext)).ToList();
            var primaries = members.Where(i => !SidecarOnly.Contains(i.Ext)).ToList();
            if (primaries.Count(IsJpgLike) == 1)
            {
                var dngs = primaries.Where(i => i.Ext.Equals("dng", StringComparison.OrdinalIgnoreCase)).ToList();
                primaries = primaries.Except(dngs).ToList();
                sidecars.AddRange(dngs);
            }
            if (primaries.Count == 0) { foreach (var s in sidecars) result.Add((s, new())); continue; }
            result.Add((primaries[0], sidecars));
            foreach (var p in primaries.Skip(1)) result.Add((p, new()));
        }
        return result;
    }
}
