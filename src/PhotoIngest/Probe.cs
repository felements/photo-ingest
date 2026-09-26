namespace PhotoIngest;

public static class Probe
{
    public static int Run(Options o)
    {
        foreach (var f in o.Positional)
        {
            var full = Path.GetFullPath(f);
            var name = Path.GetFileName(full);
            var size = new FileInfo(full).Length;
            var (bucket, reason) = Rules.Classify(name, size);
            var meta = Meta.FromMetadata(full, out var msrc);
            var mtime = File.GetLastWriteTime(full);
            var chosen = Resolve.For(full, name, _ => null, mtime);
            Console.WriteLine($"{full}");
            Console.WriteLine($"  bucket={bucket} ({reason}) size={size}");
            Console.WriteLine($"  metadata={meta:yyyy-MM-dd HH:mm:ss} ({msrc})");
            Console.WriteLine($"  filename={Dates.FromFilename(name):yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  mtime={mtime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  chosen={chosen?.When:yyyy-MM-dd HH:mm:ss} ({chosen?.Source})");
        }
        return 0;
    }
}
