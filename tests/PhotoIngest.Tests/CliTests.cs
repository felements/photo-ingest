using PhotoIngest;

public class CliTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    public void HelpFlagsSucceedAndListEveryCommand(string arg)
    {
        var w = new StringWriter();
        Assert.Equal(0, Cli.Run(new[] { arg }, w, TextWriter.Null));
        var text = w.ToString();
        foreach (var cmd in new[] { "ingest", "report", "verify", "probe", "help" }) Assert.Contains(cmd, text);
        Assert.Contains("_screenshots", text);
        Assert.Contains("Pictures/archive", text);   // documents the default archive
    }

    [Fact]
    public void HelpRecapsWhatTheProgramDoes()
    {
        var w = new StringWriter();
        Cli.Run(new[] { "help" }, w, TextWriter.Null);
        var text = w.ToString();
        Assert.Contains("what it does", text);
        foreach (var step in new[] { "1.", "2.", "3.", "4.", "5.", "6." }) Assert.Contains(step, text);
        Assert.Contains("SHA-256", text);
        Assert.Contains("never deletes", text);
    }

    [Fact]
    public void UsageSynopsisCarriesATagline()
    {
        var err = new StringWriter();
        Cli.Run(new string[0], TextWriter.Null, err);
        Assert.StartsWith("photo-ingest: ", err.ToString());
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("version")]
    public void VersionPrintsSemverAndExitsZero(string arg)
    {
        var w = new StringWriter();
        Assert.Equal(0, Cli.Run(new[] { arg }, w, TextWriter.Null));
        Assert.Matches(@"^photo-ingest \d+\.\d+\.\d+", w.ToString().Trim());
    }

    [Fact]
    public void ProbeOnMissingFileReportsAndContinues()
    {
        var tmp = Path.GetTempFileName(); File.WriteAllText(tmp, "x");
        try
        {
            var out_ = new StringWriter(); var err = new StringWriter();
            var code = Cli.Run(new[] { "probe", "/nonexistent/IMG_20230705_201022.jpg", tmp }, out_, err);
            Assert.Equal(1, code);
            Assert.Contains("not found", err.ToString());
            Assert.Contains(tmp, out_.ToString());          // the existing file was still probed
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public void HelpForOneCommandExplainsItsOptions()
    {
        var w = new StringWriter();
        Assert.Equal(0, Cli.Run(new[] { "help", "ingest" }, w, TextWriter.Null));
        Assert.Contains("--dry-run", w.ToString());
        Assert.Contains("--event", w.ToString());
        Assert.DoesNotContain("--undated", w.ToString());
    }

    [Fact]
    public void CommandDashDashHelpShowsThatCommand()
    {
        var w = new StringWriter();
        Assert.Equal(0, Cli.Run(new[] { "report", "--help" }, w, TextWriter.Null));
        Assert.Contains("--undated", w.ToString());
        Assert.DoesNotContain("--event", w.ToString());
    }

    [Fact]
    public void HelpForUnknownCommandIsAnError()
    {
        var err = new StringWriter();
        Assert.Equal(2, Cli.Run(new[] { "help", "bogus" }, TextWriter.Null, err));
        Assert.Contains("bogus", err.ToString());
        Assert.Contains("usage:", err.ToString());
    }

    [Fact]
    public void NoArgumentsPrintsUsageToStderr()
    {
        var err = new StringWriter();
        Assert.Equal(2, Cli.Run(new string[0], TextWriter.Null, err));
        Assert.Contains("usage:", err.ToString());
    }

    [Fact]
    public void UnknownOptionIsRejectedWithUsage()
    {
        var err = new StringWriter();
        Assert.Equal(2, Cli.Run(new[] { "report", "--bogus" }, TextWriter.Null, err));
        Assert.Contains("--bogus", err.ToString());
        Assert.Contains("usage:", err.ToString());
    }

    [Fact]
    public void MissingOptionValueIsRejectedWithUsage()
    {
        var err = new StringWriter();
        Assert.Equal(2, Cli.Run(new[] { "report", "--archive" }, TextWriter.Null, err));
        Assert.Contains("--archive", err.ToString());
    }
}
