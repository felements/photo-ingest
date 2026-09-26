using PhotoIngest;

/// <summary>Test double: records every phase and status a command reports.</summary>
public sealed class RecordingProgress : IProgress
{
    public sealed record Phase(string Name, bool Bytes) { public long Total; public long Advanced; public bool Done; }
    public readonly List<Phase> Phases = new();
    public readonly List<string> Statuses = new();

    public IPhase Start(string name, long total, bool bytes = false)
    {
        var p = new Phase(name, bytes) { Total = total };
        lock (Phases) Phases.Add(p);
        return new Handle(p);
    }
    public void Status(string text) { lock (Statuses) Statuses.Add(text); }
    public void Dispose() { }

    sealed class Handle : IPhase
    {
        readonly Phase p;
        public Handle(Phase p) => this.p = p;
        public void Advance(long n = 1) => Interlocked.Add(ref p.Advanced, n);
        public void SetTotal(long total) => Interlocked.Exchange(ref p.Total, total);
        public void Done() => p.Done = true;
        public void Dispose() => Done();
    }
}

public class ProgressTests
{
    [Fact]
    public void FactoryPicksPlainWhenNotATerminal() => Assert.IsType<PlainProgress>(Progress.Create(TextWriter.Null, isTerminal: false));

    [Fact]
    public void FactoryPicksLiveWhenATerminal() { using var p = Progress.Create(TextWriter.Null, isTerminal: true); Assert.IsType<SpectreProgress>(p); }

    [Fact]
    public void PlainWritesOneLinePerFinishedPhase()
    {
        var w = new StringWriter();
        using (var p = Progress.Create(w, isTerminal: false))
        {
            using (var walk = p.Start("walking", 0)) { walk.Advance(1234); }
            using (var hash = p.Start("hashing", 3_000_000_000, bytes: true)) { hash.Advance(3_000_000_000); }
            var copy = p.Start("copying", 10); copy.Advance(4); copy.Done();
            p.Status("copy 4  duplicate 6");
        }
        var lines = w.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("walking: 1,234", lines[0]);
        Assert.Equal("hashing: 3.0 GB", lines[1]);
        Assert.Equal("copying: 4 / 10", lines[2]);
        Assert.Equal("copy 4  duplicate 6", lines[3]);
        Assert.Equal(4, lines.Length);
    }

    [Fact]
    public void PlainOnlyKeepsTheLastStatus()
    {
        var w = new StringWriter();
        using (var p = Progress.Create(w, isTerminal: false)) { p.Status("first"); p.Status("second"); }
        Assert.Equal("second", w.ToString().Trim());
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(999, "999 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(95_400_000_000, "95.4 GB")]
    [InlineData(1_200_000_000_000, "1.2 TB")]
    public void FormatsBytes(long n, string expected) => Assert.Equal(expected, Progress.Bytes(n));
}
