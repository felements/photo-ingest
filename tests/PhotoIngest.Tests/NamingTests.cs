using PhotoIngest;

public class NamingTests
{
    static readonly DateTime D = new(2023, 11, 4, 18, 30, 12);

    [Fact] public void Prefix() => Assert.Equal("20231104_183012_", Naming.Prefix(D));
    [Fact] public void MediaDir() => Assert.Equal("2023/2023-11", Naming.MonthDir(Bucket.Media, D, null));
    [Fact] public void MediaDirWithEvent() => Assert.Equal("2016/2016-06 georgia", Naming.MonthDir(Bucket.Media, new DateTime(2016, 6, 3), "georgia"));
    [Fact] public void ScreenshotsDir() => Assert.Equal("_screenshots/2023-11", Naming.MonthDir(Bucket.Screenshots, D, "ignored"));
    [Fact] public void WhatsAppDir() => Assert.Equal("_whatsapp/2023-11", Naming.MonthDir(Bucket.WhatsApp, D, null));
    [Fact] public void OtherBucketThrows() => Assert.Throws<ArgumentException>(() => Naming.MonthDir(Bucket.NonMedia, D, null));

    [Fact]
    public void ReservesAndSuffixes()
    {
        var reserved = new HashSet<string>(StringComparer.Ordinal);
        var a = Naming.Dest("2023/2023-11", "20231104_183012_", "IMG_1.jpg", reserved, _ => false, _ => false);
        var b = Naming.Dest("2023/2023-11", "20231104_183012_", "IMG_1.jpg", reserved, _ => false, _ => false);
        var c = Naming.Dest("2023/2023-11", "20231104_183012_", "IMG_1.jpg", reserved, _ => false, _ => false);
        Assert.Equal("2023/2023-11/20231104_183012_IMG_1.jpg", a);
        Assert.Equal("2023/2023-11/20231104_183012_IMG_1~1.jpg", b);
        Assert.Equal("2023/2023-11/20231104_183012_IMG_1~2.jpg", c);
    }

    [Fact] public void EmptyPrefixKeepsName() => Assert.Equal("_nonmedia/dump/Books/x.fb2", Naming.Dest("_nonmedia/dump/Books", "", "x.fb2", new HashSet<string>(), _ => false, _ => false));

    [Fact]
    public void ExistingDifferentContentGetsSuffix()
        => Assert.Equal("d/p_a~1.jpg", Naming.Dest("d", "p_", "a.jpg", new HashSet<string>(), rel => rel == "d/p_a.jpg", _ => false));

    [Fact]
    public void ExistingSameContentIsReused()
    {
        var reserved = new HashSet<string>();
        Assert.Equal("d/p_a.jpg", Naming.Dest("d", "p_", "a.jpg", reserved, rel => rel == "d/p_a.jpg", rel => rel == "d/p_a.jpg"));
        Assert.Contains("d/p_a.jpg", reserved);
    }

    [Fact] public void NoExtension() => Assert.Equal("d/p_README", Naming.Dest("d", "p_", "README", new HashSet<string>(), _ => false, _ => false));
    [Fact] public void MultiDotKeepsLastExtOnly() => Assert.Equal("d/p_PXL.RAW-02.ORIGINAL~1.dng", Naming.Dest("d", "p_", "PXL.RAW-02.ORIGINAL.dng", new HashSet<string> { "d/p_PXL.RAW-02.ORIGINAL.dng" }, _ => false, _ => false));
}
