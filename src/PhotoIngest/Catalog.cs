using Microsoft.Data.Sqlite;

namespace PhotoIngest;

public sealed class Catalog : IDisposable
{
    readonly SqliteConnection db;

    const string Schema = """
        CREATE TABLE IF NOT EXISTS files (
            id INTEGER PRIMARY KEY, sha256 TEXT NOT NULL UNIQUE, size INTEGER NOT NULL, dest_rel TEXT NOT NULL UNIQUE,
            bucket TEXT NOT NULL, taken_at TEXT, date_source TEXT NOT NULL, dump TEXT NOT NULL, src_rel TEXT NOT NULL,
            ingested_at TEXT NOT NULL, run_id INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS duplicates (
            id INTEGER PRIMARY KEY, sha256 TEXT NOT NULL, dump TEXT NOT NULL, src_rel TEXT NOT NULL, size INTEGER NOT NULL,
            of_file_id INTEGER NOT NULL, run_id INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS runs (
            id INTEGER PRIMARY KEY, started_at TEXT NOT NULL, finished_at TEXT, dump TEXT NOT NULL, source_root TEXT NOT NULL,
            event TEXT, dry_run INTEGER NOT NULL, counts_json TEXT);
        CREATE INDEX IF NOT EXISTS files_dump_src ON files(dump, src_rel);
        CREATE INDEX IF NOT EXISTS dups_dump_src ON duplicates(dump, src_rel);
        """;

    public Catalog(string path)
    {
        db = new SqliteConnection($"Data Source={path}");
        db.Open();
        if (path != ":memory:") Exec("PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;");
        Exec(Schema);
    }

    static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    SqliteCommand Cmd(string sql, (string name, object? value)[] p)
    {
        var c = db.CreateCommand();
        c.CommandText = sql;
        foreach (var (k, v) in p) c.Parameters.AddWithValue(k, v ?? DBNull.Value);
        return c;
    }

    void Exec(string sql, params (string name, object? value)[] p) { using var c = Cmd(sql, p); c.ExecuteNonQuery(); }
    object? Scalar(string sql, params (string name, object? value)[] p) { using var c = Cmd(sql, p); return c.ExecuteScalar(); }
    long LastId() => (long)Scalar("SELECT last_insert_rowid()")!;

    public List<object?[]> Query(string sql, params (string name, object? value)[] p)
    {
        using var c = Cmd(sql, p);
        using var r = c.ExecuteReader();
        var rows = new List<object?[]>();
        while (r.Read())
        {
            var row = new object?[r.FieldCount];
            for (int i = 0; i < r.FieldCount; i++) row[i] = r.IsDBNull(i) ? null : r.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    public long Count(string table) => (long)Scalar($"SELECT count(*) FROM {table}")!;

    public long BeginRun(string dump, string sourceRoot, string? evt, bool dryRun)
    {
        Exec("INSERT INTO runs(started_at,dump,source_root,event,dry_run) VALUES($s,$d,$r,$e,$y)",
            ("$s", Now()), ("$d", dump), ("$r", sourceRoot), ("$e", evt), ("$y", dryRun ? 1 : 0));
        return LastId();
    }

    public void FinishRun(long runId, string countsJson)
        => Exec("UPDATE runs SET finished_at=$f, counts_json=$c WHERE id=$i", ("$f", Now()), ("$c", countsJson), ("$i", runId));

    /// <summary>Fast path: the same file (dump name, relative path, size) from the same source root was recorded by an earlier run.</summary>
    public bool AlreadySeen(string dump, string srcRel, long size, string sourceRoot)
        => Scalar("""
            SELECT 1 FROM files f JOIN runs r ON r.id=f.run_id WHERE f.dump=$d AND f.src_rel=$s AND f.size=$z AND r.source_root=$root
            UNION ALL
            SELECT 1 FROM duplicates x JOIN runs r ON r.id=x.run_id WHERE x.dump=$d AND x.src_rel=$s AND x.size=$z AND r.source_root=$root
            LIMIT 1
            """, ("$d", dump), ("$s", srcRel), ("$z", size), ("$root", sourceRoot)) != null;

    public bool HasDest(string destRel) => Scalar("SELECT 1 FROM files WHERE dest_rel=$d", ("$d", destRel)) != null;

    public (long id, string destRel)? FindByHash(string sha)
    {
        var rows = Query("SELECT id, dest_rel FROM files WHERE sha256=$h", ("$h", sha));
        return rows.Count == 0 ? null : ((long)rows[0][0]!, (string)rows[0][1]!);
    }

    public long AddFile(string sha, long size, string destRel, Bucket bucket, DateTime? takenAt, DateSource source, string dump, string srcRel, long runId)
    {
        Exec("INSERT INTO files(sha256,size,dest_rel,bucket,taken_at,date_source,dump,src_rel,ingested_at,run_id) VALUES($h,$z,$d,$b,$t,$s,$u,$r,$n,$i)",
            ("$h", sha), ("$z", size), ("$d", destRel), ("$b", bucket.ToString()), ("$t", takenAt?.ToString("yyyy-MM-dd HH:mm:ss")),
            ("$s", source.ToString().ToLowerInvariant()), ("$u", dump), ("$r", srcRel), ("$n", Now()), ("$i", runId));
        return LastId();
    }

    public void AddDuplicate(string sha, string dump, string srcRel, long size, long ofFileId, long runId)
        => Exec("INSERT INTO duplicates(sha256,dump,src_rel,size,of_file_id,run_id) VALUES($h,$d,$s,$z,$o,$r)",
            ("$h", sha), ("$d", dump), ("$s", srcRel), ("$z", size), ("$o", ofFileId), ("$r", runId));

    public IEnumerable<(string sha, string destRel, long size)> AllFilesWithSize()
        => Query("SELECT sha256, dest_rel, size FROM files ORDER BY dest_rel").Select(r => ((string)r[0]!, (string)r[1]!, (long)r[2]!));

    public IEnumerable<(string sha, string destRel)> AllFiles()
        => Query("SELECT sha256, dest_rel FROM files ORDER BY dest_rel").Select(r => ((string)r[0]!, (string)r[1]!));

    public void Dispose() => db.Dispose();
}
