using PhotoIngest;

public class ForgetTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "forget-" + Guid.NewGuid());
    readonly string dump, archive;

    public ForgetTests()
    {
        dump = Path.Combine(root, "d"); archive = Path.Combine(root, "archive");
        W("DCIM/IMG_20230705_201022.jpg", "a");
        W("DCIM/IMG_20230705_201023.jpg", "b");
        W("misc-cloud/takeout-1.tgz", "big tarball one");
        W("misc-cloud/takeout-2.tgz", "big tarball two");
        Assert.Equal(0, Ingest.Run(new Options { Command = "ingest", Positional = { dump }, Archive = archive }));
    }

    void W(string rel, string content) { var p = Path.Combine(dump, rel); Directory.CreateDirectory(Path.GetDirectoryName(p)!); File.WriteAllText(p, content); }
    Catalog Cat() => new(Path.Combine(archive, "_index.sqlite"));
    int Run(StringWriter w, params string[] patterns) => Forget.Run(new Options { Command = "forget", Archive = archive, Positional = new(patterns) }, w, TextWriter.Null);
    int RunDelete(StringWriter w, params string[] patterns) => Forget.Run(new Options { Command = "forget", Archive = archive, Positional = new(patterns), Delete = true }, w, TextWriter.Null);
    const string Tgz1 = "_nonmedia/d/misc/misc-cloud/takeout-1.tgz";
    const string Tgz2 = "_nonmedia/d/misc/misc-cloud/takeout-2.tgz";

    [Fact]
    public void WithoutDeleteOnlyLists()
    {
        var w = new StringWriter();
        Assert.Equal(0, Run(w, "_nonmedia/d/misc/misc-cloud/*.tgz"));
        Assert.Contains(Tgz1, w.ToString()); Assert.Contains(Tgz2, w.ToString());
        Assert.Contains("--delete", w.ToString());                          // tells the user how to actually do it
        Assert.True(File.Exists(Path.Combine(archive, Tgz1)));
        using var c = Cat(); Assert.Equal(4, c.Count("files")); Assert.Equal(0, c.Count("dropped"));
    }

    [Fact]
    public void DeleteOneExactPath()
    {
        Assert.Equal(0, RunDelete(new StringWriter(), Tgz1));
        Assert.False(File.Exists(Path.Combine(archive, Tgz1)));
        Assert.True(File.Exists(Path.Combine(archive, Tgz2)));
        using var c = Cat(); Assert.Equal(3, c.Count("files")); Assert.Equal(1, c.Count("dropped"));
        Assert.Equal(3, File.ReadAllLines(Path.Combine(archive, "_manifest.sha256")).Length);
        var (missing, changed, untracked) = Verify.Check(archive);
        Assert.Empty(missing); Assert.Empty(changed); Assert.Empty(untracked);
    }

    [Fact]
    public void DirectoryPathForgetsEverythingBelowAndPrunesIt()
    {
        Assert.Equal(0, RunDelete(new StringWriter(), "_nonmedia/d/misc/misc-cloud"));
        Assert.False(Directory.Exists(Path.Combine(archive, "_nonmedia/d/misc/misc-cloud")));
        Assert.False(Directory.Exists(Path.Combine(archive, "_nonmedia/d")));      // emptied parents go too
        Assert.True(Directory.Exists(Path.Combine(archive, "2023")));
        using var c = Cat(); Assert.Equal(2, c.Count("files")); Assert.Equal(2, c.Count("dropped"));
    }

    [Fact]
    public void AbsolutePathInsideArchiveIsAccepted()
    {
        Assert.Equal(0, RunDelete(new StringWriter(), Path.Combine(archive, Tgz2)));
        Assert.False(File.Exists(Path.Combine(archive, Tgz2)));
    }

    [Fact]
    public void NoMatchIsAnError()
    {
        var err = new StringWriter();
        Assert.Equal(1, Forget.Run(new Options { Command = "forget", Archive = archive, Positional = { "nothing/here/*" } }, TextWriter.Null, err));
        Assert.Contains("no archived file matches", err.ToString());
    }

    [Fact]
    public void ReingestingForgottenBytesIsLoggedAsDroppedNotCopied()
    {
        Assert.Equal(0, RunDelete(new StringWriter(), Tgz1));
        // same bytes arrive again from a different dump
        var dump2 = Path.Combine(root, "d2", "x"); Directory.CreateDirectory(dump2);
        File.WriteAllText(Path.Combine(dump2, "renamed.tgz"), "big tarball one");
        Assert.Equal(0, Ingest.Run(new Options { Command = "ingest", Positional = { Path.Combine(root, "d2") }, Archive = archive }));
        Assert.False(File.Exists(Path.Combine(archive, Tgz1)));
        Assert.Empty(Directory.GetFiles(archive, "renamed.tgz", SearchOption.AllDirectories));
        var log = Directory.GetFiles(Path.Combine(archive, "_runs")).OrderBy(f => f).Last();
        Assert.Contains(File.ReadLines(log), l => l.StartsWith("dropped\t") && l.Contains("renamed.tgz"));
        using var c = Cat(); Assert.Equal(3, c.Count("files"));
    }

    [Fact]
    public void ReingestingTheSameDumpUsesTheFastPathForForgottenFiles()
    {
        Assert.Equal(0, RunDelete(new StringWriter(), Tgz1));
        Assert.Equal(0, Ingest.Run(new Options { Command = "ingest", Positional = { dump }, Archive = archive }));
        var log = Directory.GetFiles(Path.Combine(archive, "_runs")).OrderBy(f => f).Last();
        Assert.Contains(File.ReadLines(log), l => l.StartsWith("already\t") && l.Contains("takeout-1.tgz"));
        Assert.False(File.Exists(Path.Combine(archive, Tgz1)));
    }

    [Fact]
    public void ReportShowsDroppedCount()
    {
        RunDelete(new StringWriter(), Tgz1);
        var w = new StringWriter();
        Report.Run(new Options { Command = "report", Archive = archive }, w);
        Assert.Contains("dropped 1", w.ToString());
    }

    public void Dispose() { try { Directory.Delete(root, true); } catch { } }
}
