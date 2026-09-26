using PhotoIngest;

public class VerifyTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "verify-" + Guid.NewGuid());
    readonly string dump, archive;

    public VerifyTests()
    {
        dump = Path.Combine(root, "d"); archive = Path.Combine(root, "archive");
        Directory.CreateDirectory(Path.Combine(dump, "DCIM"));
        File.WriteAllText(Path.Combine(dump, "DCIM", "IMG_20230705_201022.jpg"), "a");
        File.WriteAllText(Path.Combine(dump, "DCIM", "IMG_20230705_201023.jpg"), "b");
        Directory.CreateDirectory(Path.Combine(dump, "Books"));
        File.WriteAllText(Path.Combine(dump, "Books", "x.fb2"), "book");
        Assert.Equal(0, Ingest.Run(new Options { Command = "ingest", Positional = { dump }, Archive = archive }));
    }

    [Fact]
    public void ManifestMatchesSha256sumFormat()
    {
        var lines = File.ReadAllLines(Path.Combine(archive, "_manifest.sha256"));
        Assert.Equal(3, lines.Length);
        Assert.Equal("ca978112ca1bbdcafac231b39a23dc4da786eff8147c4e72b9807785afee48bb  2023/2023-07/20230705_201022_IMG_20230705_201022.jpg", lines[0]);
    }

    [Fact]
    public void CleanArchiveVerifies()
    {
        var (missing, changed, untracked) = Verify.Check(archive);
        Assert.Empty(missing); Assert.Empty(changed); Assert.Empty(untracked);
        Assert.Equal(0, Verify.Run(new Options { Command = "verify", Archive = archive }));
    }

    [Fact]
    public void DetectsMissingChangedUntracked()
    {
        File.Delete(Path.Combine(archive, "2023/2023-07/20230705_201022_IMG_20230705_201022.jpg"));
        File.WriteAllText(Path.Combine(archive, "2023/2023-07/20230705_201023_IMG_20230705_201023.jpg"), "CHANGED");
        File.WriteAllText(Path.Combine(archive, "2023/stray.txt"), "x");
        var (missing, changed, untracked) = Verify.Check(archive);
        Assert.Equal(new[] { "2023/2023-07/20230705_201022_IMG_20230705_201022.jpg" }, missing);
        Assert.Equal(new[] { "2023/2023-07/20230705_201023_IMG_20230705_201023.jpg" }, changed);
        Assert.Equal(new[] { "2023/stray.txt" }, untracked);
        Assert.Equal(1, Verify.Run(new Options { Command = "verify", Archive = archive }));
    }

    [Fact]
    public void UndatedReportExcludesParkedNonMedia()
    {
        var w = new StringWriter();
        Assert.Equal(0, Report.Run(new Options { Command = "report", Archive = archive, Undated = true }, w));
        Assert.Contains("0 files in _undated", w.ToString());
        var full = new StringWriter();
        Assert.Equal(0, Report.Run(new Options { Command = "report", Archive = archive }, full));
        Assert.DoesNotContain("(undated)", full.ToString());
        Assert.Contains("parked", full.ToString());
    }

    [Fact]
    public void VerifyReportsPhases()
    {
        var rec = new RecordingProgress();
        Verify.Check(archive, rec);
        Assert.Equal(new[] { "hashing", "scanning" }, rec.Phases.Select(p => p.Name));
        Assert.Equal(6, rec.Phases[0].Total);   // "a" + "b" + "book"
        Assert.Equal(6, rec.Phases[0].Advanced);
        Assert.True(rec.Phases[0].Bytes);
        Assert.All(rec.Phases, p => Assert.True(p.Done));
    }

    [Fact]
    public void ReportRuns()
    {
        Assert.Equal(0, Report.Run(new Options { Command = "report", Archive = archive }));
        Assert.Equal(0, Report.Run(new Options { Command = "report", Archive = archive, Source = "filename" }));
        Assert.Equal(0, Report.Run(new Options { Command = "report", Archive = archive, Undated = true }));
    }

    public void Dispose() { try { Directory.Delete(root, true); } catch { } }
}
