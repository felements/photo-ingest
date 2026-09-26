using PhotoIngest;

public class ResolveTests
{
    // Paths do not exist, so metadata yields null and the rest of the chain is exercised.
    static DateGuess? R(string rel, DateTime? sidecar = null, DateTime? mtime = null)
        => Resolve.For("/nonexistent/" + rel, rel, _ => sidecar, mtime ?? new DateTime(1980, 1, 1));

    [Fact] public void SidecarBeatsFilename() => Assert.Equal(DateSource.Sidecar, R("Photos from 2019/IMG_20230705_201022.jpg", sidecar: new DateTime(2019, 5, 1))!.Source);
    [Fact] public void Filename() { var g = R("DCIM/IMG_20230705_201022.jpg")!; Assert.Equal(DateSource.Filename, g.Source); Assert.Equal(new DateTime(2023, 7, 5, 20, 10, 22), g.When); }
    [Fact] public void Folder() => Assert.Equal(DateSource.Folder, R("lrraw/2015/2015-08-26/x.jpg")!.Source);
    [Fact] public void Mtime() { var g = R("DCIM/x.jpg", mtime: new DateTime(2012, 3, 4, 5, 6, 7))!; Assert.Equal(DateSource.Mtime, g.Source); Assert.Equal(new DateTime(2012, 3, 4, 5, 6, 7), g.When); }
    [Fact] public void MtimeTooOldIsNull() => Assert.Null(R("DCIM/x.jpg", mtime: new DateTime(1999, 12, 31)));
    [Fact] public void MissingFileMetadataIsNull() { Assert.Null(Meta.FromMetadata("/nonexistent/x.jpg", out var s)); Assert.Equal(DateSource.None, s); }
    [Fact] public void GarbageFileMetadataIsNull()
    {
        var tmp = Path.GetTempFileName(); File.WriteAllText(tmp, "not an image");
        try { Assert.Null(Meta.FromMetadata(tmp, out var s)); Assert.Equal(DateSource.None, s); } finally { File.Delete(tmp); }
    }
}
