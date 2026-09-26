using PhotoIngest;

public class SidecarsTests
{
    static Item It(string rel) => new() { Rel = rel, Full = "/x/" + rel, Size = 1, Bucket = Bucket.Media, Ext = Rules.Ext(rel), Stem = Sidecars.Stem(Path.GetFileName(rel)) };

    [Theory]
    [InlineData("IMG_1.jpg", "IMG_1")]
    [InlineData("PXL_3.RAW-02.ORIGINAL.dng", "PXL_3")]
    [InlineData("README", "README")]
    public void Stem(string name, string stem) => Assert.Equal(stem, Sidecars.Stem(name));

    [Fact] public void JpgPlusDng()
    {
        var g = Sidecars.Group(new[] { It("IMG_1.dng"), It("IMG_1.jpg") });
        Assert.Single(g); Assert.Equal("IMG_1.jpg", g[0].primary.Rel); Assert.Equal("IMG_1.dng", Assert.Single(g[0].sidecars).Rel);
    }
    [Fact] public void LoneDngIsPrimary() { var g = Sidecars.Group(new[] { It("IMG_2.dng") }); Assert.Equal("IMG_2.dng", g[0].primary.Rel); Assert.Empty(g[0].sidecars); }
    [Fact] public void PixelRawAndMotion() { var g = Sidecars.Group(new[] { It("PXL_3.jpg"), It("PXL_3.RAW-02.ORIGINAL.dng"), It("PXL_3.MP") }); Assert.Single(g); Assert.Equal(2, g[0].sidecars.Count); }
    [Fact] public void OrfTifPp3()
    {
        var g = Sidecars.Group(new[] { It("P1.ORF"), It("P1.ORF.pp3"), It("P1.tif") });
        Assert.Equal(2, g.Count); Assert.Equal("P1.ORF", g.Single(x => x.sidecars.Count == 1).primary.Rel);
    }
    [Fact] public void ThmFollowsMp4() { var g = Sidecars.Group(new[] { It("MOV1.thm"), It("MOV1.mp4") }); Assert.Equal("MOV1.mp4", g[0].primary.Rel); Assert.Single(g[0].sidecars); }
    [Fact] public void SameNumericStemStaysSeparate()
    {
        var g = Sidecars.Group(new[] { It("2009.09-a.jpg"), It("2009.09-b.jpg") });
        Assert.Equal(2, g.Count); Assert.Equal(0, g.Sum(x => x.sidecars.Count));
    }
    [Fact] public void CaseInsensitive() { var g = Sidecars.Group(new[] { It("img_5.JPG"), It("IMG_5.DNG") }); Assert.Single(g); Assert.Equal("img_5.JPG", g[0].primary.Rel); }
    [Fact] public void LoneSidecarStandsAlone() { var g = Sidecars.Group(new[] { It("lonely.thm") }); Assert.Equal("lonely.thm", g[0].primary.Rel); }
}
