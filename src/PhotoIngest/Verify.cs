namespace PhotoIngest;

public static class Verify
{
    static readonly string[] Internal = { "_index.sqlite", "_index.sqlite-wal", "_index.sqlite-shm", "_manifest.sha256", "_manifest.sha256.part" };

    public static (List<string> missing, List<string> changed, List<string> untracked) Check(string archive) => Check(archive, Progress.None);

    public static (List<string> missing, List<string> changed, List<string> untracked) Check(string archive, IProgress progress)
    {
        var missing = new List<string>(); var changed = new List<string>(); var untracked = new List<string>();
        var tracked = new HashSet<string>(StringComparer.Ordinal);
        using (var cat = new Catalog(Path.Combine(archive, "_index.sqlite")))
        {
            var all = cat.AllFilesWithSize().ToList();
            using var hashing = progress.Start("hashing", all.Sum(f => f.size), bytes: true);
            foreach (var (sha, rel, size) in all)
            {
                tracked.Add(rel);
                var full = Path.Combine(archive, rel);
                if (!File.Exists(full)) missing.Add(rel);
                else if (Hashing.Sha256(full) != sha) changed.Add(rel);
                hashing.Advance(size);
                if ((missing.Count + changed.Count) % 10 == 1) progress.Status($"missing {missing.Count}  changed {changed.Count}");
            }
        }
        using (var scanning = progress.Start("scanning", 0))
            foreach (var full in Directory.EnumerateFiles(archive, "*", SearchOption.AllDirectories))
            {
                scanning.Advance();
                var rel = Path.GetRelativePath(archive, full).Replace('\\', '/');
                if (rel.StartsWith("_runs/") || Internal.Contains(rel)) continue;
                if (!tracked.Contains(rel)) untracked.Add(rel);
            }
        untracked.Sort(StringComparer.Ordinal);
        progress.Status($"missing {missing.Count}  changed {changed.Count}  untracked {untracked.Count}");
        return (missing, changed, untracked);
    }

    public static int Run(Options o) => Run(o, Progress.None);

    public static int Run(Options o, IProgress progress)
    {
        var archive = Path.GetFullPath(o.Archive);
        if (!File.Exists(Path.Combine(archive, "_index.sqlite"))) { Console.Error.WriteLine($"no _index.sqlite in {archive}"); return 2; }
        var (missing, changed, untracked) = Check(archive, progress);
        Print("missing", missing); Print("changed", changed); Print("untracked", untracked);
        Console.WriteLine($"verify: {missing.Count} missing, {changed.Count} changed, {untracked.Count} untracked");
        return missing.Count + changed.Count + untracked.Count == 0 ? 0 : 1;
    }

    static void Print(string label, List<string> items) { foreach (var i in items) Console.WriteLine($"{label}\t{i}"); }
}
