using System.Text;

namespace PhotoIngest;

public static class Ingest
{
    sealed class Plan
    {
        public Item Primary = null!;
        public List<Item> Sidecars = new();
        public DateTime? When;
        public DateSource Source = DateSource.None;
    }

    static readonly HashSet<Bucket> Dated = new() { Bucket.Media, Bucket.Screenshots, Bucket.WhatsApp };

    public static List<Item> Walk(string root) => Walk(root, new List<string>());

    public static List<Item> Walk(string root, List<string> unreadable) => Walk(root, unreadable, null);

    /// <summary>All regular files under root, sorted by relative path. Directories or files that cannot be read are appended to <paramref name="unreadable"/> as relative paths.</summary>
    public static List<Item> Walk(string root, List<string> unreadable, Action? tick)
    {
        var items = new List<Item>();
        WalkDir(root, root, items, unreadable, tick);
        items.Sort((a, b) => string.CompareOrdinal(a.Rel, b.Rel));
        return items;
    }

    static string RelOf(string root, string full) => Path.GetRelativePath(root, full).Replace('\\', '/');

    static void WalkDir(string dir, string root, List<Item> items, List<string> unreadable, Action? tick)
    {
        List<string> entries;
        try { entries = Directory.EnumerateFileSystemEntries(dir).ToList(); }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException) { unreadable.Add(RelOf(root, dir)); return; }
        foreach (var full in entries)
        {
            FileInfo fi;
            try
            {
                fi = new FileInfo(full);
                if (fi.LinkTarget is not null) continue;                       // symlinks are never followed
                if ((fi.Attributes & FileAttributes.Directory) != 0) { WalkDir(full, root, items, unreadable, tick); continue; }
                var rel = RelOf(root, full);
                var name = Path.GetFileName(rel);
                var (bucket, reason) = Rules.Classify(rel, fi.Length);
                items.Add(new Item { Rel = rel, Full = full, Size = fi.Length, Mtime = fi.LastWriteTime, Bucket = bucket, Reason = reason, Ext = Rules.Ext(name), Stem = Sidecars.Stem(name) });
                tick?.Invoke();
            }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException) { unreadable.Add(RelOf(root, full)); }
        }
    }

    static string DirOf(string rel) => (Path.GetDirectoryName(rel) ?? "").Replace('\\', '/');

    public static int Run(Options o) => Run(o, Progress.None);

    public static int Run(Options o, IProgress progress)
    {
        if (o.Positional.Count != 1) { Cli.Usage(); return 2; }
        var root = Path.GetFullPath(o.Positional[0].TrimEnd('/'));
        if (!Directory.Exists(root)) { Console.Error.WriteLine($"not a directory: {root}"); return 2; }
        var dump = Path.GetFileName(root);
        var archive = Path.GetFullPath(o.Archive);
        Directory.CreateDirectory(Path.Combine(archive, "_runs"));

        var logPath = Path.Combine(archive, "_runs", $"{DateTime.Now:yyyy-MM-ddTHH-mm-ss}_{dump}{(o.DryRun ? "_dry" : "")}.log");
        using var log = new StreamWriter(logPath, false, new UTF8Encoding(false));
        using var cat = new Catalog(Path.Combine(archive, "_index.sqlite"));
        var counts = new Counts();
        var runId = o.DryRun ? -1 : cat.BeginRun(dump, root, o.Event, false);
        Console.Error.WriteLine($"ingest '{dump}' -> {archive}{(o.DryRun ? " (dry run)" : "")}\nlog: {logPath}");

        void Log(string action, Item it, Plan? p, string target)
            => log.WriteLine($"{action}\t{it.Bucket}\t{(p is null ? "" : p.Source.ToString().ToLowerInvariant())}\t{p?.When:yyyy-MM-dd HH:mm:ss}\t{it.Rel}\t{target}");

        // 1. walk + classify
        var unreadable = new List<string>();
        var walking = progress.Start("walking", 0);
        var items = Walk(root, unreadable, () => walking.Advance());
        walking.Done();
        foreach (var u in unreadable) { counts.Add("error"); log.WriteLine($"error\t\t\t\t{u}\tunreadable"); }

        // 2. Takeout sidecar dates
        var takeout = Takeout.Build(root, items.Where(i => i.Ext.Equals("json", StringComparison.OrdinalIgnoreCase)).Select(i => i.Rel));

        // 3. skip + fast path
        var fresh = new List<Item>();
        foreach (var it in items)
        {
            if (it.Bucket == Bucket.Skip) { counts.Add("skip", it.Bucket); Log("skip", it, null, it.Reason); continue; }
            if (!o.DryRun && cat.AlreadySeen(dump, it.Rel, it.Size, root)) { counts.Add("already", it.Bucket); Log("already", it, null, ""); continue; }
            fresh.Add(it);
        }

        // 4. plans
        var plans = new List<Plan>();
        foreach (var dir in fresh.Where(i => Dated.Contains(i.Bucket)).GroupBy(i => DirOf(i.Rel)))
            foreach (var (p, s) in Sidecars.Group(dir)) plans.Add(new Plan { Primary = p, Sidecars = s });
        foreach (var it in fresh.Where(i => !Dated.Contains(i.Bucket))) plans.Add(new Plan { Primary = it });
        plans.Sort((a, b) => string.CompareOrdinal(a.Primary.Rel, b.Primary.Rel));

        var par = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount / 2) };

        // 5. dates
        var datedPlans = plans.Where(p => Dated.Contains(p.Primary.Bucket)).ToList();
        using (var dating = progress.Start("dating", datedPlans.Count))
            Parallel.ForEach(datedPlans, par, p =>
            {
                var g = Resolve.For(p.Primary.Full, p.Primary.Rel, rel => takeout.TryGetValue(rel, out var d) ? d : null, p.Primary.Mtime);
                p.When = g?.When; p.Source = g?.Source ?? DateSource.None;
                dating.Advance();
            });

        // 6. hashes
        var toHash = plans.SelectMany(p => p.Sidecars.Prepend(p.Primary)).ToList();
        using (var hashing = progress.Start("hashing", toHash.Sum(i => i.Size), bytes: true))
            Parallel.ForEach(toHash, par, it =>
            {
                try { it.Sha = Hashing.Sha256(it.Full); }
                catch (Exception e) { it.Sha = null; it.Reason = "ERR " + e.Message; }
                hashing.Advance(it.Size);
            });

        // 7. sequential placement
        var reserved = new HashSet<string>(StringComparer.Ordinal);
        var seenInRun = new Dictionary<string, (long id, string dest)>(StringComparer.Ordinal);
        var planned = new Dictionary<Bucket, List<string>>();
        var copying = progress.Start("copying", toHash.Sum(i => i.Size), bytes: true);
        int nCopy = 0, nDup = 0, nErr = 0, nSkip = counts.Get("skip"), nDone = 0;
        void Tick(Item it) { copying.Advance(it.Size); if (++nDone % 50 == 0) progress.Status($"copy {nCopy}  duplicate {nDup}  skip {nSkip}  error {nErr}"); }
        try
        {
            foreach (var p in plans)
            {
                var prim = p.Primary;
                string dir = prim.Bucket switch
                {
                    Bucket.NonMedia => $"_nonmedia/{dump}/{DirOf(prim.Rel)}",
                    Bucket.Misc => $"_nonmedia/{dump}/misc/{DirOf(prim.Rel)}",
                    _ when p.When is null => $"_undated/{dump}/{DirOf(prim.Rel)}",
                    _ => Naming.MonthDir(prim.Bucket, p.When!.Value, o.Event),
                };
                dir = dir.TrimEnd('/');
                var prefix = p.When is DateTime w && Dated.Contains(prim.Bucket) ? Naming.Prefix(w) : "";

                foreach (var it in p.Sidecars.Prepend(prim))
                {
                    if (it.Sha is null) { counts.Add("error", it.Bucket); Log("error", it, p, it.Reason); nErr++; Tick(it); continue; }

                    if (seenInRun.TryGetValue(it.Sha, out var seen))
                    {
                        counts.Add("duplicate", it.Bucket); Log("duplicate", it, p, seen.dest);
                        if (!o.DryRun) cat.AddDuplicate(it.Sha, dump, it.Rel, it.Size, seen.id, runId);
                        nDup++; Tick(it); continue;
                    }
                    if (cat.FindByHash(it.Sha) is (long ofId, string ofDest))
                    {
                        counts.Add("duplicate", it.Bucket); Log("duplicate", it, p, ofDest);
                        if (!o.DryRun) cat.AddDuplicate(it.Sha, dump, it.Rel, it.Size, ofId, runId);
                        nDup++; Tick(it); continue;
                    }
                    if (cat.FindDropped(it.Sha) is string droppedAs)
                    {
                        // these bytes were archived once and deliberately forgotten; do not bring them back
                        counts.Add("dropped", it.Bucket); Log("dropped", it, p, droppedAs);
                        nDup++; Tick(it); continue;
                    }

                    // a name is taken if a file is on disk or the catalog still points at it (e.g. moved out by hand)
                    var dest = Naming.Dest(dir, prefix, Path.GetFileName(it.Rel), reserved,
                        rel => File.Exists(Path.Combine(archive, rel)) || cat.HasDest(rel),
                        rel => File.Exists(Path.Combine(archive, rel)) && Hashing.Sha256(Path.Combine(archive, rel)) == it.Sha);
                    counts.Add("copy", it.Bucket); counts.Add("copy", p.Source); Log("copy", it, p, dest);
                    if (!planned.TryGetValue(it.Bucket, out var list)) planned[it.Bucket] = list = new();
                    if (list.Count < 50) list.Add($"{it.Rel} -> {dest}");

                    long id = -1;
                    if (!o.DryRun)
                    {
                        var full = Path.Combine(archive, dest);
                        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                        if (!File.Exists(full))
                        {
                            var tmp = full + ".part";
                            try
                            {
                                File.Copy(it.Full, tmp, true);
                                File.SetLastWriteTimeUtc(tmp, File.GetLastWriteTimeUtc(it.Full));
                                if (Hashing.Sha256(tmp) != it.Sha) throw new IOException($"copy verification failed: {it.Rel}");
                                File.Move(tmp, full, true);
                            }
                            catch { if (File.Exists(tmp)) File.Delete(tmp); throw; }
                        }
                        id = cat.AddFile(it.Sha, it.Size, dest, it.Bucket, p.When, p.Source, dump, it.Rel, runId);
                    }
                    seenInRun[it.Sha] = (id, dest);
                    nCopy++; Tick(it);
                }
            }
        }
        catch (Exception e)
        {
            copying.Done();
            log.WriteLine($"abort\t\t\t\t\t{e.Message}");
            log.Flush();
            Console.Error.WriteLine($"ABORTED: {e.Message}\nlog: {logPath}");
            return 1;
        }

        progress.Status($"copy {nCopy}  duplicate {nDup}  skip {nSkip}  error {nErr}");
        copying.Done();
        if (!o.DryRun) { cat.FinishRun(runId, counts.ToJson()); Manifest.Write(cat, archive); }
        counts.Print(Console.Out);
        if (o.DryRun)
            foreach (var (b, list) in planned) { Console.WriteLine($"planned {b} (first {list.Count}):"); foreach (var l in list) Console.WriteLine("  " + l); }
        return 0;
    }
}
