using System.Globalization;
using System.Text.RegularExpressions;

namespace PhotoIngest;

public enum DateSource { Exif, Video, Sidecar, Filename, Folder, Mtime, None }
public record DateGuess(DateTime When, DateSource Source);

public static class Dates
{
    static readonly Regex[] FullPatterns =
    {
        new(@"(?<!\d)(?<y>\d{4})(?<mo>\d{2})(?<d>\d{2})[_-](?<h>\d{2})(?<mi>\d{2})(?<s>\d{2})"),
        new(@"WP_(?<y>\d{4})(?<mo>\d{2})(?<d>\d{2})_(?<h>\d{2})_(?<mi>\d{2})_(?<s>\d{2})", RegexOptions.IgnoreCase),
        new(@"(?<!\d)(?<y>\d{4})-(?<mo>\d{2})-(?<d>\d{2})[ _T-](?<h>\d{2})[-.:](?<mi>\d{2})[-.:](?<s>\d{2})"),
    };
    static readonly Regex[] DayPatterns =
    {
        new(@"(?:IMG|VID|PTT|AUD)-(?<y>\d{4})(?<mo>\d{2})(?<d>\d{2})-WA", RegexOptions.IgnoreCase),
        new(@"(?<!\d)(?<y>\d{4})-(?<mo>\d{2})-(?<d>\d{2})(?!\d)"),
        new(@"(?<!\d)(?<y>\d{4})(?<mo>\d{2})(?<d>\d{2})(?!\d)"),
    };
    static readonly Regex[] FolderPatterns =
    {
        new(@"(?<!\d)(?<y>\d{4})-(?<mo>\d{2})-(?<d>\d{2})(?!\d)"),
        new(@"(?<!\d)(?<y>\d{4})\.(?<mo>\d{2})(?!\d)"),
        new(@"Photos from (?<y>\d{4})"),
    };
    static readonly string[] ExifFormats = { "yyyy:MM:dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy:MM:dd" };

    static bool Build(Match m, out DateTime dt)
    {
        dt = default;
        if (!m.Success) return false;
        int G(string g, int dflt) => m.Groups[g].Success ? int.Parse(m.Groups[g].Value) : dflt;
        int y = G("y", 0), mo = G("mo", 1), d = G("d", 1), h = G("h", 0), mi = G("mi", 0), s = G("s", 0);
        if (y < 1990 || y > 2100 || mo < 1 || mo > 12 || d < 1 || d > DateTime.DaysInMonth(y, mo) || h > 23 || mi > 59 || s > 59) return false;
        dt = new DateTime(y, mo, d, h, mi, s);
        return true;
    }

    public static DateTime? FromFilename(string name)
    {
        foreach (var r in FullPatterns) if (Build(r.Match(name), out var dt)) return dt;
        foreach (var r in DayPatterns) if (Build(r.Match(name), out var dt)) return dt;
        return null;
    }

    public static DateTime? FromFolders(string relPath)
    {
        var segs = relPath.Split('/');
        for (int i = segs.Length - 2; i >= 0; i--)
            foreach (var r in FolderPatterns) if (Build(r.Match(segs[i]), out var dt)) return dt;
        return null;
    }

    public static bool ParseExif(string s, out DateTime dt)
    {
        dt = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        return DateTime.TryParseExact(s.Trim(), ExifFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt) && dt.Year >= 1990;
    }
}
