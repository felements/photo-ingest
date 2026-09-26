using System.Globalization;
using Spectre.Console;

namespace PhotoIngest;

/// <summary>Progress reporting for long commands. Phases are started in sequence; Advance may be called from several threads.</summary>
public interface IProgress : IDisposable
{
    /// <param name="total">expected amount (files, or bytes when <paramref name="bytes"/>); 0 = unknown</param>
    IPhase Start(string name, long total, bool bytes = false);
    /// <summary>One line of counters shown next to the running phase; the last one is kept.</summary>
    void Status(string text);
}

public interface IPhase : IDisposable
{
    void Advance(long n = 1);
    void SetTotal(long total);
    void Done();
}

public static class Progress
{
    public static IProgress Create(TextWriter err, bool isTerminal) => isTerminal ? new SpectreProgress(err) : new PlainProgress(err);
    public static IProgress None => new PlainProgress(TextWriter.Null);

    public static string Bytes(long n)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
        double v = n; int u = 0;
        while (v >= 1000 && u < units.Length - 1) { v /= 1000; u++; }
        return u == 0 ? $"{n} B" : v.ToString("0.0", CultureInfo.InvariantCulture) + " " + units[u];
    }

    public static string Count(long n) => n.ToString("N0", CultureInfo.InvariantCulture);

    internal static string Amount(long value, long total, bool bytes)
    {
        var one = bytes ? Bytes(value) : Count(value);
        return total > 0 && value < total ? $"{one} / {(bytes ? Bytes(total) : Count(total))}" : one;
    }
}

/// <summary>Fallback when stderr is not a terminal: one line per finished phase, plus the last status line.</summary>
public sealed class PlainProgress : IProgress
{
    readonly TextWriter w;
    string? lastStatus;
    public PlainProgress(TextWriter w) => this.w = w;

    public IPhase Start(string name, long total, bool bytes = false) => new Phase(this, name, total, bytes);
    public void Status(string text) => lastStatus = text;
    public void Dispose() { if (lastStatus is not null) w.WriteLine(lastStatus); w.Flush(); }

    sealed class Phase : IPhase
    {
        readonly PlainProgress owner; readonly string name; readonly bool bytes; long total, value; bool done;
        public Phase(PlainProgress o, string n, long t, bool b) { owner = o; name = n; total = t; bytes = b; }
        public void Advance(long n = 1) => Interlocked.Add(ref value, n);
        public void SetTotal(long t) => Interlocked.Exchange(ref total, t);
        public void Done() { if (done) return; done = true; owner.w.WriteLine($"{name}: {Progress.Amount(value, total, bytes)}"); }
        public void Dispose() => Done();
    }
}

/// <summary>Live progress bars on a terminal via Spectre.Console, rendered to stderr.</summary>
public sealed class SpectreProgress : IProgress
{
    readonly TaskCompletionSource finished = new();
    readonly Task renderer;
    ProgressContext ctx = null!;
    Phase? current;
    string status = "";

    public SpectreProgress(TextWriter err)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(err), Interactive = InteractionSupport.Yes });
        var ready = new ManualResetEventSlim();
        renderer = console.Progress()
            .AutoClear(false).HideCompleted(false)
            .Columns(new TaskDescriptionColumn { Alignment = Justify.Left }, new ProgressBarColumn(), new PercentageColumn(), new RemainingTimeColumn(), new SpinnerColumn())
            .StartAsync(async c => { ctx = c; ready.Set(); await finished.Task; });
        ready.Wait();
    }

    public IPhase Start(string name, long total, bool bytes = false)
    {
        var p = new Phase(this, ctx.AddTask(name, maxValue: Math.Max(1, total)), name, total, bytes);
        current = p;
        p.Render();
        return p;
    }

    public void Status(string text) { status = text; current?.Render(); }

    public void Dispose()
    {
        current?.Done();
        finished.TrySetResult();
        renderer.Wait();
    }

    sealed class Phase : IPhase
    {
        readonly SpectreProgress owner; readonly ProgressTask task; readonly string name; readonly bool bytes;
        long total, value; int renderGate; bool done;
        public Phase(SpectreProgress o, ProgressTask t, string n, long tot, bool b) { owner = o; task = t; name = n; total = tot; bytes = b; if (tot <= 0) task.IsIndeterminate = true; }

        public void Advance(long n = 1)
        {
            var v = Interlocked.Add(ref value, n);
            task.Value = Math.Min(v, task.MaxValue);
            if (Interlocked.Increment(ref renderGate) % 16 == 0) Render();
        }

        public void SetTotal(long t) { Interlocked.Exchange(ref total, t); task.MaxValue = Math.Max(1, t); task.IsIndeterminate = t <= 0; Render(); }

        public void Render()
        {
            var suffix = ReferenceEquals(owner.current, this) && owner.status.Length > 0 ? "   " + owner.status : "";
            task.Description = Markup.Escape($"{name,-9} {Progress.Amount(Interlocked.Read(ref value), Interlocked.Read(ref total), bytes)}{suffix}");
        }

        public void Done()
        {
            if (done) return; done = true;
            task.IsIndeterminate = false;
            if (total <= 0) task.MaxValue = Math.Max(1, value);
            task.Value = task.MaxValue;
            Render();
            task.StopTask();
        }

        public void Dispose() => Done();
    }
}
