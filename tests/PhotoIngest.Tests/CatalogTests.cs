using PhotoIngest;

public class CatalogTests
{
    [Fact]
    public void Sha256OfKnownContent()
    {
        var tmp = Path.GetTempFileName(); File.WriteAllText(tmp, "abc");
        try { Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", Hashing.Sha256(tmp)); }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public void RoundTrip()
    {
        using var c = new Catalog(":memory:");
        var run = c.BeginRun("dump A", "/src/dump A", null, false);
        Assert.True(run > 0);
        Assert.False(c.AlreadySeen("dump A", "DCIM/a.jpg", 10, "/src/dump A"));
        Assert.Null(c.FindByHash("aa"));
        Assert.False(c.HasDest("2023/2023-11/x_a.jpg"));

        var id = c.AddFile("aa", 10, "2023/2023-11/x_a.jpg", Bucket.Media, new DateTime(2023, 11, 4, 18, 30, 12), DateSource.Exif, "dump A", "DCIM/a.jpg", run);
        Assert.True(c.AlreadySeen("dump A", "DCIM/a.jpg", 10, "/src/dump A"));
        Assert.False(c.AlreadySeen("dump A", "DCIM/a.jpg", 10, "/elsewhere/dump A"));
        Assert.False(c.AlreadySeen("dump A", "DCIM/a.jpg", 11, "/src/dump A"));
        Assert.False(c.AlreadySeen("dump B", "DCIM/a.jpg", 10, "/src/dump A"));
        Assert.True(c.HasDest("2023/2023-11/x_a.jpg"));
        var found = c.FindByHash("aa");
        Assert.Equal((id, "2023/2023-11/x_a.jpg"), found);

        c.AddDuplicate("aa", "dump B", "copy/a.jpg", 10, id, run);
        Assert.True(c.AlreadySeen("dump B", "copy/a.jpg", 10, "/src/dump A"));
        Assert.Equal(1, c.Count("duplicates"));

        c.AddFile("bb", 5, "_undated/dump A/x.jpg", Bucket.Media, null, DateSource.None, "dump A", "x.jpg", run);
        Assert.Equal(new[] { ("aa", "2023/2023-11/x_a.jpg"), ("bb", "_undated/dump A/x.jpg") }, c.AllFiles().ToArray());

        c.FinishRun(run, "{\"copy\":2}");
        var rows = c.Query("SELECT counts_json, finished_at FROM runs WHERE id=$i", ("$i", run));
        Assert.Single(rows); Assert.Equal("{\"copy\":2}", rows[0][0]); Assert.NotNull(rows[0][1]);

        var stored = c.Query("SELECT taken_at, date_source, bucket FROM files WHERE sha256='aa'")[0];
        Assert.Equal("2023-11-04 18:30:12", stored[0]); Assert.Equal("exif", stored[1]); Assert.Equal("Media", stored[2]);
        Assert.Null(c.Query("SELECT taken_at FROM files WHERE sha256='bb'")[0][0]);
    }

    [Fact]
    public void DuplicateHashIsRejected()
    {
        using var c = new Catalog(":memory:");
        var run = c.BeginRun("d", "/d", null, false);
        c.AddFile("aa", 1, "x/a", Bucket.Media, null, DateSource.None, "d", "a", run);
        Assert.ThrowsAny<Exception>(() => c.AddFile("aa", 1, "x/b", Bucket.Media, null, DateSource.None, "d", "b", run));
    }

    [Fact]
    public void PersistsToFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "cat-" + Guid.NewGuid() + ".sqlite");
        try
        {
            using (var c = new Catalog(path)) { var r = c.BeginRun("d", "/d", null, false); c.AddFile("aa", 1, "x/a", Bucket.Media, null, DateSource.None, "d", "a", r); }
            using (var c = new Catalog(path)) Assert.Equal(1, c.Count("files"));
        }
        finally { foreach (var f in Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + "*")) File.Delete(f); }
    }
}
