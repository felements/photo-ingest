namespace PhotoIngest;

public static class Report
{
    public static int Run(Options o) => Run(o, Console.Out);

    public static int Run(Options o, TextWriter w)
    {
        var archive = Path.GetFullPath(o.Archive);
        var dbPath = Path.Combine(archive, "_index.sqlite");
        if (!File.Exists(dbPath)) { Console.Error.WriteLine($"no _index.sqlite in {archive}"); return 2; }
        using var cat = new Catalog(dbPath);

        if (o.Undated)
        {
            var rows = cat.Query(@"SELECT dest_rel, dump, src_rel FROM files WHERE dest_rel LIKE '\_undated/%' ESCAPE '\' ORDER BY dest_rel");
            foreach (var r in rows) w.WriteLine($"{r[0]}\t{r[1]}\t{r[2]}");
            w.WriteLine($"{rows.Count} files in _undated");
            return 0;
        }
        if (o.Source is not null)
        {
            var src = o.Source.ToLowerInvariant();
            var rows = cat.Query("SELECT dest_rel, dump, src_rel FROM files WHERE date_source=$s ORDER BY dest_rel", ("$s", src));
            foreach (var r in rows) w.WriteLine($"{r[0]}\t{r[1]}\t{r[2]}");
            w.WriteLine($"{rows.Count} files with date_source={src}");
            return 0;
        }

        w.WriteLine("month\tbucket\tfiles");
        foreach (var r in cat.Query("SELECT coalesce(substr(taken_at,1,7),'_undated'), bucket, count(*) FROM files WHERE bucket IN ('Media','Screenshots','WhatsApp') GROUP BY 1,2 ORDER BY 1,2"))
            w.WriteLine($"{r[0]}\t{r[1]}\t{r[2]}");
        foreach (var r in cat.Query("SELECT bucket, count(*) FROM files WHERE bucket IN ('NonMedia','Misc') GROUP BY 1 ORDER BY 1"))
            w.WriteLine($"(parked)\t{r[0]}\t{r[1]}");
        w.WriteLine();
        w.WriteLine("date_source\tfiles");
        foreach (var r in cat.Query("SELECT date_source, count(*) FROM files GROUP BY 1 ORDER BY 2 DESC"))
            w.WriteLine($"{r[0]}\t{r[1]}");
        w.WriteLine();
        w.WriteLine("dump\tfiles\tduplicates");
        foreach (var r in cat.Query("SELECT d.dump, (SELECT count(*) FROM files f WHERE f.dump=d.dump), (SELECT count(*) FROM duplicates x WHERE x.dump=d.dump) FROM (SELECT DISTINCT dump FROM files UNION SELECT DISTINCT dump FROM duplicates) d ORDER BY 1"))
            w.WriteLine($"{r[0]}\t{r[1]}\t{r[2]}");
        w.WriteLine();
        w.WriteLine($"total files {cat.Count("files")}, duplicates {cat.Count("duplicates")}, dropped {cat.Count("dropped")}, runs {cat.Count("runs")}");
        return 0;
    }
}
