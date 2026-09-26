namespace PhotoIngest;

public static class Probe
{
    public static int Run(Options o) => Run(o, Console.Out, Console.Error);

    public static int Run(Options o, TextWriter w, TextWriter err)
    {
        if (o.Positional.Count == 0) { Cli.Usage(err); return 2; }
        var missing = 0;
        foreach (var f in o.Positional)
        {
            var full = Path.GetFullPath(f);
            if (!File.Exists(full)) { err.WriteLine($"{full}: not found"); missing++; continue; }
            var name = Path.GetFileName(full);
            var size = new FileInfo(full).Length;
            // directory rules (@eaDir, Trash, Books, WhatsApp…) are applied to the whole path, as a dump rooted above would see them
            var (bucket, reason) = Rules.Classify(full.Replace('\\', '/'), size);
            var meta = Meta.FromMetadata(full, out var msrc);
            var mtime = File.GetLastWriteTime(full);
            var chosen = Resolve.For(full, name, _ => null, mtime);
            w.WriteLine($"{full}");
            w.WriteLine($"  bucket={bucket} ({reason}) size={size}");
            w.WriteLine($"  metadata={meta:yyyy-MM-dd HH:mm:ss} ({msrc})");
            w.WriteLine($"  filename={Dates.FromFilename(name):yyyy-MM-dd HH:mm:ss}");
            w.WriteLine($"  mtime={mtime:yyyy-MM-dd HH:mm:ss}");
            w.WriteLine($"  chosen={chosen?.When:yyyy-MM-dd HH:mm:ss} ({chosen?.Source})");
        }
        return missing == 0 ? 0 : 1;
    }
}
