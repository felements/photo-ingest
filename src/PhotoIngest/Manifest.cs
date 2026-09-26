using System.Text;

namespace PhotoIngest;

public static class Manifest
{
    public static void Write(Catalog cat, string archive)
    {
        var tmp = Path.Combine(archive, "_manifest.sha256.part");
        using (var w = new StreamWriter(tmp, false, new UTF8Encoding(false)))
            foreach (var (sha, rel) in cat.AllFiles()) w.WriteLine($"{sha}  {rel}");
        File.Move(tmp, Path.Combine(archive, "_manifest.sha256"), true);
    }
}
