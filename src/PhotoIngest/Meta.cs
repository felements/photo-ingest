using MetadataExtractor;
using MetadataExtractor.Formats.Avi;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;

namespace PhotoIngest;

public static class Meta
{
    public static DateTime? FromMetadata(string path, out DateSource source)
    {
        source = DateSource.None;
        IReadOnlyList<MetadataExtractor.Directory> dirs;
        try { dirs = ImageMetadataReader.ReadMetadata(path); }
        catch { return null; }

        foreach (var d in dirs.OfType<ExifSubIfdDirectory>())
            if (TryExif(d, ExifDirectoryBase.TagDateTimeOriginal, out var dt) || TryExif(d, ExifDirectoryBase.TagDateTimeDigitized, out dt))
            { source = DateSource.Exif; return dt; }
        foreach (var d in dirs.OfType<ExifIfd0Directory>())
            if (TryExif(d, ExifDirectoryBase.TagDateTime, out var dt)) { source = DateSource.Exif; return dt; }
        foreach (var d in dirs.OfType<QuickTimeMovieHeaderDirectory>())
            if (d.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out var dt) && dt.Year > 1990)
            { source = DateSource.Video; return DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime(); }
        foreach (var d in dirs.OfType<AviDirectory>())
            if (d.TryGetDateTime(AviDirectory.TagDateTimeOriginal, out var dt) && dt.Year > 1990)
            { source = DateSource.Video; return dt; }
        return null;
    }

    static bool TryExif(MetadataExtractor.Directory d, int tag, out DateTime dt)
    {
        dt = default;
        var s = d.GetString(tag);
        return s is not null && Dates.ParseExif(s, out dt);
    }
}
