using System.Text.Json;
using System.Text.RegularExpressions;

namespace PhotoIngest;

public static class Takeout
{
    static readonly Regex StripJson = new(@"\.json$", RegexOptions.IgnoreCase);
    static readonly Regex StripSupplemental = new(@"\.supp[a-z-]*$", RegexOptions.IgnoreCase);

    public static Dictionary<string, DateTime> Build(string root, IEnumerable<string> relJsonPaths)
    {
        var map = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        foreach (var rel in relJsonPaths)
        {
            string text;
            try { text = File.ReadAllText(Path.Combine(root, rel)); } catch { continue; }
            AddJson(rel, text, map);
        }
        return map;
    }

    public static void AddJson(string relJsonPath, string json, Dictionary<string, DateTime> map)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return;
            if (!r.TryGetProperty("photoTakenTime", out var ptt) || !ptt.TryGetProperty("timestamp", out var tsEl)) return;
            if (!long.TryParse(tsEl.GetString(), out var ts)) return;
            var when = DateTimeOffset.FromUnixTimeSeconds(ts).ToLocalTime().DateTime;
            var dir = (Path.GetDirectoryName(relJsonPath) ?? "").Replace('\\', '/');
            if (r.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() is { Length: > 0 } title)
                map[Join(dir, title)] = when;
            var stem = StripSupplemental.Replace(StripJson.Replace(Path.GetFileName(relJsonPath), ""), "");
            if (stem.Contains('.')) map.TryAdd(Join(dir, stem), when);
        }
        catch (JsonException) { }
    }

    static string Join(string dir, string name) => dir.Length == 0 ? name : dir + "/" + name;
}
