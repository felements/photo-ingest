using System.Globalization;

namespace PhotoIngest;

public static class Naming
{
    public static string Prefix(DateTime dt) => dt.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "_";

    public static string MonthDir(Bucket b, DateTime dt, string? evt)
    {
        var ym = dt.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var y = dt.ToString("yyyy", CultureInfo.InvariantCulture);
        return b switch
        {
            Bucket.Media => $"{y}/{ym}" + (string.IsNullOrWhiteSpace(evt) ? "" : " " + evt.Trim()),
            Bucket.Screenshots => $"_screenshots/{ym}",
            Bucket.WhatsApp => $"_whatsapp/{ym}",
            _ => throw new ArgumentException($"no month dir for bucket {b}"),
        };
    }

    public static string Dest(string dir, string prefix, string originalName, ISet<string> reserved, Func<string, bool> existsOnDisk, Func<string, bool> sameContent)
    {
        var ext = Path.GetExtension(originalName);
        var stem = originalName[..^ext.Length];
        for (int n = 0; ; n++)
        {
            var candidate = $"{dir}/{prefix}{stem}{(n == 0 ? "" : "~" + n)}{ext}";
            if (reserved.Contains(candidate)) continue;
            if (existsOnDisk(candidate) && !sameContent(candidate)) continue;
            reserved.Add(candidate);
            return candidate;
        }
    }
}
