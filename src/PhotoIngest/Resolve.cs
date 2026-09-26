namespace PhotoIngest;

public static class Resolve
{
    public static DateGuess? For(string fullPath, string relPath, Func<string, DateTime?> sidecar, DateTime mtime)
    {
        var name = Path.GetFileName(relPath);
        var meta = Meta.FromMetadata(fullPath, out var msrc);
        if (meta is DateTime e && msrc == DateSource.Exif) return new(e, DateSource.Exif);
        var fname = Dates.FromFilename(name);
        if (meta is DateTime v && msrc == DateSource.Video)
            return fname is DateTime f && f.TimeOfDay != TimeSpan.Zero ? new(f, DateSource.Filename) : new(v, DateSource.Video);
        if (sidecar(relPath) is DateTime s) return new(s, DateSource.Sidecar);
        if (fname is DateTime f2) return new(f2, DateSource.Filename);
        if (Dates.FromFolders(relPath) is DateTime d) return new(d, DateSource.Folder);
        if (mtime.Year >= 2000) return new(mtime, DateSource.Mtime);
        return null;
    }
}
