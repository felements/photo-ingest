using PhotoIngest;

public class IngestTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "ingest-" + Guid.NewGuid());
    readonly string dump;     // "dump test" with a space, on purpose
    readonly string archive;

    public IngestTests()
    {
        dump = Path.Combine(root, "dump test");
        archive = Path.Combine(root, "archive");
        W("DCIM/Camera/IMG_20230705_201022.jpg", "a");
        W("DCIM/Camera/IMG_20230705_201022.dng", "b");
        W("DCIM/Camera/IMG_20230705_201022.jpg.pp3", "p");              // RawTherapee sidecar
        W("DCIM/Camera/copy/IMG_20230705_201022.jpg", "a");          // exact duplicate
        W("Screenshots/Screenshot From 2026-06-21 11-04-10.png", "c");
        W("WhatsApp Images/IMG-20150826-WA0002.jpg", "d");
        W("Books/x.fb2", "e");
        W("Trash/x.jpg", "f");
        W("noname.jpg", "g");                                          // mtime fallback
        File.SetLastWriteTime(Path.Combine(dump, "noname.jpg"), new DateTime(2012, 3, 4, 5, 6, 7));
        W("weird.mp~2", "h");                                          // misc
        W("empty.jpg", "");                                            // zero bytes: skipped
        W("Photos from 2019/IMG_1.jpg", "i");
        W("Photos from 2019/IMG_1.jpg.json", """{"title":"IMG_1.jpg","photoTakenTime":{"timestamp":"1546300800"}}""");
    }

    void W(string rel, string content)
    {
        var p = Path.Combine(dump, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, content);
    }

    Options Opt(bool dry, string? evt = null) => new() { Command = "ingest", Positional = { dump + "/" }, Archive = archive, DryRun = dry, Event = evt };
    bool Has(string rel) => File.Exists(Path.Combine(archive, rel));
    static string Ym(DateTime d) => d.ToString("yyyy-MM");

    [Fact]
    public void DryRunWritesNothingButLog()
    {
        Assert.Equal(0, Ingest.Run(Opt(dry: true)));
        var files = Directory.GetFiles(archive, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(archive, f)).ToList();
        Assert.All(files, f => Assert.True(f.StartsWith("_runs/") || f.StartsWith("_index.sqlite"), f));
        Assert.Contains(files, f => f.StartsWith("_runs/") && f.Contains("dump test") && f.EndsWith("_dry.log"));
        using var c = new Catalog(Path.Combine(archive, "_index.sqlite"));
        Assert.Equal(0, c.Count("files")); Assert.Equal(0, c.Count("runs"));
    }

    [Fact]
    public void RealRunPlacesEverything()
    {
        // simulate an interrupted earlier run: destination exists with the same content, no catalog row
        var pre = Path.Combine(archive, "_whatsapp/2015-08/20150826_000000_IMG-20150826-WA0002.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(pre)!); File.WriteAllText(pre, "d");

        Assert.Equal(0, Ingest.Run(Opt(dry: false)));

        Assert.True(Has("2023/2023-07/20230705_201022_IMG_20230705_201022.jpg"));
        Assert.True(Has("2023/2023-07/20230705_201022_IMG_20230705_201022.dng"));
        Assert.True(Has("2023/2023-07/20230705_201022_IMG_20230705_201022.jpg.pp3"));
        Assert.False(Directory.Exists(Path.Combine(archive, "_nonmedia/dump test/misc/DCIM")));
        Assert.True(Has("_screenshots/2026-06/20260621_110410_Screenshot From 2026-06-21 11-04-10.png"));
        Assert.True(Has("_whatsapp/2015-08/20150826_000000_IMG-20150826-WA0002.jpg"));
        Assert.False(Has("_whatsapp/2015-08/20150826_000000_IMG-20150826-WA0002~1.jpg"));
        Assert.True(Has("_nonmedia/dump test/Books/x.fb2"));
        Assert.True(Has("_nonmedia/dump test/Photos from 2019/IMG_1.jpg.json"));
        Assert.True(Has("_nonmedia/dump test/misc/weird.mp~2"));
        Assert.True(Has("2012/2012-03/20120304_050607_noname.jpg"));
        var takeoutWhen = DateTimeOffset.FromUnixTimeSeconds(1546300800).ToLocalTime().DateTime;
        Assert.True(Has($"{takeoutWhen:yyyy}/{Ym(takeoutWhen)}/{Naming.Prefix(takeoutWhen)}IMG_1.jpg"));
        Assert.False(Directory.Exists(Path.Combine(archive, "_undated")));
        Assert.Empty(Directory.GetFiles(archive, "*.part", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(archive, "*Trash*", SearchOption.AllDirectories));
        Assert.True(File.Exists(Path.Combine(archive, "_manifest.sha256")));

        using var c = new Catalog(Path.Combine(archive, "_index.sqlite"));
        Assert.Equal(10, c.Count("files"));
        Assert.Equal(1, c.Count("duplicates"));
        Assert.Equal(1, c.Count("runs"));
        var runRow = c.Query("SELECT dump, finished_at FROM runs")[0];
        Assert.Equal("dump test", runRow[0]); Assert.NotNull(runRow[1]);
        Assert.Equal("sidecar", c.Query("SELECT date_source FROM files WHERE src_rel='Photos from 2019/IMG_1.jpg'")[0][0]);
        Assert.Equal("mtime", c.Query("SELECT date_source FROM files WHERE src_rel='noname.jpg'")[0][0]);
        // sidecar dng carries the primary's date
        Assert.Equal("2023-07-05 20:10:22", c.Query("SELECT taken_at FROM files WHERE src_rel='DCIM/Camera/IMG_20230705_201022.dng'")[0][0]);
        // mtime preserved on copy
        Assert.Equal(new DateTime(2012, 3, 4, 5, 6, 7), File.GetLastWriteTime(Path.Combine(archive, "2012/2012-03/20120304_050607_noname.jpg")));
    }

    [Fact]
    public void SecondRunIsIdempotentAndSecondDumpDedupes()
    {
        Assert.Equal(0, Ingest.Run(Opt(dry: false)));
        Assert.Equal(0, Ingest.Run(Opt(dry: false)));
        using (var c = new Catalog(Path.Combine(archive, "_index.sqlite")))
        {
            Assert.Equal(10, c.Count("files")); Assert.Equal(1, c.Count("duplicates")); Assert.Equal(2, c.Count("runs"));
        }
        var dump2 = Path.Combine(root, "dump2");
        Directory.CreateDirectory(Path.Combine(dump2, "x"));
        File.WriteAllText(Path.Combine(dump2, "x", "renamed.jpg"), "a");     // same bytes as the first jpg
        File.WriteAllText(Path.Combine(dump2, "x", "IMG_20230705_201022.jpg"), "NEW"); // same name, new bytes
        Assert.Equal(0, Ingest.Run(new Options { Command = "ingest", Positional = { dump2 }, Archive = archive, Event = "trip" }));
        using (var c = new Catalog(Path.Combine(archive, "_index.sqlite")))
        {
            Assert.Equal(11, c.Count("files")); Assert.Equal(2, c.Count("duplicates"));
        }
        Assert.True(Has("2023/2023-07 trip/20230705_201022_IMG_20230705_201022.jpg")); // no ~1: --event gives a different month dir
    }

    [Fact]
    public void WalkSortsAndSkipsSymlinks()
    {
        File.CreateSymbolicLink(Path.Combine(dump, "link.jpg"), Path.Combine(dump, "noname.jpg"));
        var items = Ingest.Walk(dump);
        Assert.DoesNotContain(items, i => i.Rel == "link.jpg");
        Assert.Equal(items.Select(i => i.Rel).OrderBy(r => r, StringComparer.Ordinal), items.Select(i => i.Rel));
        Assert.Contains(items, i => i.Rel == "DCIM/Camera/IMG_20230705_201022.jpg" && i.Bucket == Bucket.Media && i.Ext == "jpg" && i.Stem == "IMG_20230705_201022");
        Assert.Contains(items, i => i.Rel == "empty.jpg" && i.Bucket == Bucket.Skip);
    }

    [Fact]
    public void SameBasenameUnderDifferentRootIsNotSkippedByFastPath()
    {
        Assert.Equal(0, Ingest.Run(Opt(dry: false)));
        // a different dump with the same folder name, same relative path and same size, different bytes
        var other = Path.Combine(root, "other", "dump test", "DCIM", "Camera");
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, "IMG_20230705_201022.jpg"), "X");
        Assert.Equal(0, Ingest.Run(new Options { Command = "ingest", Positional = { Path.Combine(root, "other", "dump test") }, Archive = archive }));
        using var c = new Catalog(Path.Combine(archive, "_index.sqlite"));
        Assert.Equal(11, c.Count("files"));
        Assert.True(Has("2023/2023-07/20230705_201022_IMG_20230705_201022~1.jpg"));
        var log = Directory.GetFiles(Path.Combine(archive, "_runs")).OrderBy(f => f).Last();
        Assert.DoesNotContain("already", File.ReadAllText(log));
    }

    [Fact]
    public void FileMovedOutOfArchiveDoesNotAbortNextIngest()
    {
        Assert.Equal(0, Ingest.Run(Opt(dry: false)));
        File.Delete(Path.Combine(archive, "2023/2023-07/20230705_201022_IMG_20230705_201022.jpg")); // user moved it by hand
        var dump2 = Path.Combine(root, "dump2", "x");
        Directory.CreateDirectory(dump2);
        File.WriteAllText(Path.Combine(dump2, "IMG_20230705_201022.jpg"), "NEW2");
        Assert.Equal(0, Ingest.Run(new Options { Command = "ingest", Positional = { Path.Combine(root, "dump2") }, Archive = archive }));
        Assert.True(Has("2023/2023-07/20230705_201022_IMG_20230705_201022~1.jpg"));
    }

    [Fact]
    public void UnreadableDirectoryIsLoggedAsError()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root") return;   // permission bits do not apply
        var locked = Path.Combine(dump, "locked");
        Directory.CreateDirectory(locked);
        File.WriteAllText(Path.Combine(locked, "x.jpg"), "z");
        File.SetUnixFileMode(locked, UnixFileMode.None);
        try
        {
            var unreadable = new List<string>();
            var items = Ingest.Walk(dump, unreadable);
            Assert.Equal(new[] { "locked" }, unreadable);
            Assert.DoesNotContain(items, i => i.Rel.StartsWith("locked/"));
            Assert.Equal(0, Ingest.Run(Opt(dry: true)));
            var log = Directory.GetFiles(Path.Combine(archive, "_runs")).Single();
            Assert.Contains(File.ReadLines(log), l => l.StartsWith("error	") && l.Contains("locked"));
        }
        finally { File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
    }

    [Fact]
    public void ReportsPhasesAndStatusesToProgress()
    {
        var rec = new RecordingProgress();
        Assert.Equal(0, Ingest.Run(Opt(dry: false), rec));
        Assert.Equal(new[] { "walking", "dating", "hashing", "copying" }, rec.Phases.Select(p => p.Name));
        var hash = rec.Phases[2];
        Assert.True(hash.Bytes); Assert.True(hash.Total > 0); Assert.Equal(hash.Total, hash.Advanced);
        var copy = rec.Phases[3];
        Assert.True(copy.Bytes); Assert.Equal(copy.Total, copy.Advanced);
        Assert.All(rec.Phases, p => Assert.True(p.Done, p.Name));
        Assert.Contains(rec.Statuses, s => s.Contains("copy 10") && s.Contains("duplicate 1"));
    }

    [Fact] public void MissingDirIsUsageError() => Assert.Equal(2, Ingest.Run(new Options { Command = "ingest", Positional = { root + "/nope" }, Archive = archive }));

    public void Dispose() { try { Directory.Delete(root, true); } catch { } }
}
