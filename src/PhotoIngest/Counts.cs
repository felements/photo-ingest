using System.Text.Json;

namespace PhotoIngest;

public sealed class Counts
{
    readonly SortedDictionary<string, int> byAction = new(StringComparer.Ordinal);
    readonly SortedDictionary<string, int> bySource = new(StringComparer.Ordinal);
    readonly object gate = new();

    public void Add(string action) { lock (gate) Bump(byAction, action); }
    public void Add(string action, Bucket b) { lock (gate) { Bump(byAction, action); Bump(byAction, action + "/" + b); } }
    public void Add(string action, DateSource s) { lock (gate) Bump(bySource, action + "/" + s.ToString().ToLowerInvariant()); }
    static void Bump(SortedDictionary<string, int> d, string k) => d[k] = d.GetValueOrDefault(k) + 1;

    public int Get(string action) { lock (gate) return byAction.GetValueOrDefault(action); }

    public string ToJson() => JsonSerializer.Serialize(new { actions = byAction, sources = bySource });

    public void Print(TextWriter w)
    {
        w.WriteLine("actions:");
        foreach (var (k, v) in byAction) w.WriteLine($"  {v,8}  {k}");
        w.WriteLine("date sources (copied files):");
        foreach (var (k, v) in bySource) w.WriteLine($"  {v,8}  {k}");
    }
}
