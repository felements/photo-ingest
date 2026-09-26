using System.Text.RegularExpressions;

namespace PhotoIngest;

/// <summary>Removes archived files from the archive while remembering their hashes, so ingest never copies those bytes again.</summary>
public static class Forget
{
    public static int Run(Options o) => Run(o, Console.Out, Console.Error);

    public static int Run(Options o, TextWriter w, TextWriter err)
    {
        if (o.Positional.Count == 0) { Cli.Usage(err); return 2; }
        var archive = Path.GetFullPath(o.Archive);
        if (!File.Exists(Path.Combine(archive, "_index.sqlite"))) { err.WriteLine($"no _index.sqlite in {archive}"); return 2; }
        var apply = o.Delete && !o.DryRun;

        using var cat = new Catalog(Path.Combine(archive, "_index.sqlite"));
        var matches = new Dictionary<long, (long id, string sha, long size, string destRel, string dump, string srcRel)>();
        foreach (var pattern in o.Positional)
            foreach (var m in Match(cat, archive, pattern)) matches[m.id] = m;
        if (matches.Count == 0) { err.WriteLine($"no archived file matches {string.Join(" ", o.Positional)}"); return 1; }

        long bytes = 0;
        foreach (var m in matches.Values.OrderBy(m => m.destRel, StringComparer.Ordinal))
        {
            bytes += m.size;
            w.WriteLine($"{(apply ? "forget" : "would forget")}\t{m.destRel}\t{Progress.Bytes(m.size)}\t{m.dump}/{m.srcRel}");
            if (!apply) continue;
            var full = Path.Combine(archive, m.destRel);
            if (File.Exists(full)) File.Delete(full);
            cat.Drop(m.id);
            PruneEmptyParents(Path.GetDirectoryName(full)!, archive);
        }
        if (apply) Manifest.Write(cat, archive);
        w.WriteLine($"{matches.Count} files, {Progress.Bytes(bytes)}{(apply ? " forgotten and deleted" : "; add --delete to forget them and delete the files")}");
        return 0;
    }

    /// <summary>A pattern is a path relative to the archive (an absolute path inside it is accepted); * ? ** are wildcards; a directory means everything below it.</summary>
    static IEnumerable<(long id, string sha, long size, string destRel, string dump, string srcRel)> Match(Catalog cat, string archive, string pattern)
    {
        var p = pattern.Replace('\\', '/');
        var archivePrefix = archive.Replace('\\', '/').TrimEnd('/') + "/";
        if (p.StartsWith(archivePrefix, StringComparison.Ordinal)) p = p[archivePrefix.Length..];
        p = p.Trim('/');
        if (p.Length == 0) return Enumerable.Empty<(long, string, long, string, string, string)>();

        if (!p.Contains('*') && !p.Contains('?'))
            return cat.FilesLike(Like(p)).Concat(cat.FilesLike(Like(p) + "/%")).DistinctBy(m => m.id);

        // wildcard: narrow with the literal prefix in SQL, then apply the glob as a regex
        var literal = p.Substring(0, Math.Min(p.IndexOfAny(new[] { '*', '?' }), p.Length));
        var rx = new Regex("^" + Regex.Escape(p).Replace(@"\*\*", "\u0001").Replace(@"\*", "[^/]*").Replace("\u0001", ".*").Replace(@"\?", "[^/]") + "$");
        return cat.FilesLike(Like(literal) + "%").Where(m => rx.IsMatch(m.destRel));
    }

    static string Like(string s) => s.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

    static void PruneEmptyParents(string dir, string archive)
    {
        var root = Path.GetFullPath(archive).TrimEnd('/');
        while (dir.Length > root.Length && dir.StartsWith(root, StringComparison.Ordinal) && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
        {
            Directory.Delete(dir);
            dir = Path.GetDirectoryName(dir)!;
        }
    }
}
