using PhotoIngest;

public class OptionsTests
{
    [Fact]
    public void ParsesIngestWithAllFlags()
    {
        var o = Options.Parse(new[] { "ingest", "/x/dump", "--archive", "/a", "--event", "georgia", "--dry-run" });
        Assert.Equal("ingest", o.Command);
        Assert.Equal("/x/dump", o.Positional[0]);
        Assert.Equal("/a", o.Archive);
        Assert.Equal("georgia", o.Event);
        Assert.True(o.DryRun);
    }

    [Fact]
    public void DefaultArchiveIsPicturesArchive()
    {
        var o = Options.Parse(new[] { "report" });
        Assert.EndsWith("/Pictures/archive", o.Archive);
        Assert.False(o.DryRun);
        Assert.Null(o.Event);
    }

    [Fact]
    public void ReportFlags()
    {
        var o = Options.Parse(new[] { "report", "--undated", "--source", "mtime" });
        Assert.True(o.Undated);
        Assert.Equal("mtime", o.Source);
    }
}

public class OptionsParseErrors
{
    [Fact] public void DeleteFlag() { Assert.True(Options.Parse(new[] { "forget", "x", "--delete" }).Delete); Assert.False(Options.Parse(new[] { "forget", "x" }).Delete); }
    [Fact] public void HelpFlagAnywhere() { Assert.True(Options.Parse(new[] { "ingest", "/d", "--help" }).Help); Assert.True(Options.Parse(new[] { "-h" }).Help); }
    [Fact] public void UnknownFlagThrows() => Assert.Throws<OptionsException>(() => Options.Parse(new[] { "report", "--bogus" }));
    [Fact] public void MissingValueThrows() => Assert.Throws<OptionsException>(() => Options.Parse(new[] { "ingest", "/d", "--event" }));
}
