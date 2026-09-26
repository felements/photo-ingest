using PhotoIngest;

public class RulesTests
{
    static Bucket C(string p, long s = 100) => Rules.Classify(p, s).bucket;

    [Theory]
    [InlineData(".stfolder/x.jpg")]
    [InlineData("Trash/PXL_1.jpg")]
    [InlineData("DCIM/.globalTrash/x.jpg")]
    [InlineData("DCIM/.deleteRecord/x")]
    [InlineData("DCIM/.nomedia")]
    [InlineData("a/b.tmp")]
    [InlineData("a/backup~")]
    [InlineData("Family/@eaDir/x.jpg")]
    [InlineData("Family/@eaDir/PICT0979.JPG/SYNOFILE_THUMB_S.jpg")]
    [InlineData("#recycle/old.jpg")]
    [InlineData("photo/@Recycle/x.jpg")]
    [InlineData("photo/.SynologyWorkingDirectory/x")]
    [InlineData("photo/@tmp/x.jpg")]
    [InlineData("photo/.@__thumb/x.jpg")]
    [InlineData("trip/.DS_Store")]
    [InlineData("trip/._IMG_1234.jpg")]
    public void Skips(string p) => Assert.Equal(Bucket.Skip, C(p));

    [Fact] public void SkipsEmptyFile() => Assert.Equal(Bucket.Skip, C("DCIM/Camera/IMG_1.jpg", 0));

    [Theory]
    [InlineData("Books/x/y.fb2")]
    [InlineData("Photos from 2019/IMG_1.jpg.json")]
    [InlineData("Books/cover.jpg")]
    [InlineData("Download/setup.bin")]
    [InlineData("WhatsApp Audio/PTT-1.opus")]
    public void NonMedia(string p) => Assert.Equal(Bucket.NonMedia, C(p));

    [Theory]
    [InlineData("DCIM/Screenshots/a.png")]
    [InlineData("DCIM/ScreenRecorder/a.mp4")]
    [InlineData("Screenshot From 2026-06-21 11-04-10.png")]
    public void Screenshots(string p) => Assert.Equal(Bucket.Screenshots, C(p));

    [Theory]
    [InlineData("WhatsApp Images/IMG_1.jpg")]
    [InlineData("lrraw/2015/IMG-20150826-WA0002.jpg")]
    [InlineData("x/VID-20160228-WA0001.mp4")]
    public void WhatsApp(string p) => Assert.Equal(Bucket.WhatsApp, C(p));

    [Theory]
    [InlineData("DCIM/Camera/IMG_20230705_201022.jpg")]
    [InlineData("raw/P4221326.ORF")]
    [InlineData("raw/P4221326.Jpg")]
    [InlineData("Camera/IMG_1.MP")]
    [InlineData("old-photo-scan/01.xcf")]
    [InlineData("VID_20140628_111416.3gp")]
    [InlineData("trip/_IMG_1234.jpg")]
    [InlineData("raw/P4221326.ORF.pp3")]
    [InlineData("Camera roll/WP_000342.jpg.pp3")]
    [InlineData("x/MOV001.thm")]
    [InlineData("x/IMG_1.xmp")]
    public void Media(string p) => Assert.Equal(Bucket.Media, C(p));

    [Theory]
    [InlineData("x/y.mp~2")]
    [InlineData("x/README")]
    public void Misc(string p) => Assert.Equal(Bucket.Misc, C(p));

    [Fact] public void ReasonNamesTheRule() => Assert.Equal("dir:Trash", Rules.Classify("Trash/a.jpg", 5).reason);
    [Fact] public void ExtHelper() { Assert.Equal("jpg", Rules.Ext("IMG_1.jpg")); Assert.Equal("", Rules.Ext("README")); }
}
