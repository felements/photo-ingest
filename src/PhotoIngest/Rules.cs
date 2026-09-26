using System.Text.RegularExpressions;

namespace PhotoIngest;

public enum Bucket { Skip, NonMedia, Screenshots, WhatsApp, Media, Misc }

public static class Rules
{
    static readonly StringComparer IC = StringComparer.OrdinalIgnoreCase;
    static readonly HashSet<string> SkipSegments = new(IC) { ".stfolder", ".thumbnails", "@eaDir", ".globalTrash", ".deleteRecord", ".trash", "Trash", "System Volume Information" };
    static readonly HashSet<string> SkipNames = new(IC) { ".nomedia", "Thumbs.db", "desktop.ini" };
    static readonly HashSet<string> NonMediaExt = new(IC) { "fb2", "epub", "pdf", "doc", "docx", "xls", "rtf", "djvu", "mp3", "opus", "zip", "unitypackage", "crypt12", "json", "txt", "ini", "bin", "records", "nar", "tnl", "cover" };
    static readonly HashSet<string> NonMediaSegments = new(IC) { "Books", "Documents", "Download", "Downloads", "Android" };
    static readonly HashSet<string> ScreenshotSegments = new(IC) { "Screenshots", "Screenshot", "ScreenRecorder", "Screen recordings" };
    public static readonly HashSet<string> MediaExt = new(IC) { "jpg", "jpeg", "png", "gif", "heic", "heif", "webp", "bmp", "tif", "tiff", "dng", "orf", "nef", "arw", "cr2", "raf", "mp4", "mov", "3gp", "mkv", "avi", "webm", "m4v", "mp", "xcf", "pp3", "thm", "xmp", "aae", "lrv" };
    static readonly Regex WaName = new(@"^(IMG|VID|PTT|AUD)-\d{8}-WA\d+", RegexOptions.IgnoreCase);

    public static string Ext(string name)
    {
        var e = Path.GetExtension(name);
        return e.Length > 1 ? e[1..] : "";
    }

    public static (Bucket bucket, string reason) Classify(string relPath, long size)
    {
        var segs = relPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var name = segs[^1];
        var dirs = segs[..^1];
        var ext = Ext(name);
        if (size == 0) return (Bucket.Skip, "empty");
        foreach (var d in dirs) if (SkipSegments.Contains(d)) return (Bucket.Skip, "dir:" + d);
        if (SkipNames.Contains(name) || ext.Equals("tmp", StringComparison.OrdinalIgnoreCase) || name.EndsWith('~')) return (Bucket.Skip, "name");
        if (NonMediaExt.Contains(ext)) return (Bucket.NonMedia, "ext:" + ext);
        foreach (var d in dirs) if (NonMediaSegments.Contains(d)) return (Bucket.NonMedia, "dir:" + d);
        foreach (var d in dirs) if (ScreenshotSegments.Contains(d)) return (Bucket.Screenshots, "dir:" + d);
        if (name.StartsWith("Screenshot", StringComparison.OrdinalIgnoreCase)) return (Bucket.Screenshots, "name");
        foreach (var d in dirs) if (d.StartsWith("WhatsApp", StringComparison.OrdinalIgnoreCase)) return (Bucket.WhatsApp, "dir:" + d);
        if (WaName.IsMatch(name)) return (Bucket.WhatsApp, "name");
        if (MediaExt.Contains(ext)) return (Bucket.Media, "ext:" + ext);
        return (Bucket.Misc, "unmatched:" + ext);
    }
}
