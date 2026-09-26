using PhotoIngest;

public class DatesTests
{
    [Theory]
    [InlineData("IMG_20230705_201022.jpg", "2023-07-05 20:10:22")]
    [InlineData("PXL_20251211_120620277.RAW-02.ORIGINAL.dng", "2025-12-11 12:06:20")]
    [InlineData("VID_20201217_212129.mp4", "2020-12-17 21:21:29")]
    [InlineData("WP_20150826_12_34_56_Pro__highres.jpg", "2015-08-26 12:34:56")]
    [InlineData("Screenshot From 2026-06-21 11-04-10.png", "2026-06-21 11:04:10")]
    [InlineData("IMG-20150826-WA0002.jpg", "2015-08-26 00:00:00")]
    [InlineData("2021-12-24.jpg", "2021-12-24 00:00:00")]
    [InlineData("VID_20140628.3gp", "2014-06-28 00:00:00")]
    public void FilenameDates(string name, string expected)
        => Assert.Equal(DateTime.Parse(expected), Dates.FromFilename(name));

    [Theory]
    [InlineData("368872783.jpg")]
    [InlineData("IMG_20231399_120000.jpg")]
    [InlineData("70885e4c28fd2ca2d23d029fe35c874a.jpg")]
    [InlineData("02062007210.jpg")]
    [InlineData("f_2-bw.jpg")]
    [InlineData("IMG_18500101_120000.jpg")]
    public void NotDates(string name) => Assert.Null(Dates.FromFilename(name));

    [Theory]
    [InlineData("lumia/lrraw/2015/2015-08-26/x.jpg", "2015-08-26")]
    [InlineData("2021.11-dump/raw 2022.04-lake-trip/x.jpg", "2022-04-01")]
    [InlineData("google/Photos from 2019/x.jpg", "2019-01-01")]
    public void FolderDates(string rel, string expected) => Assert.Equal(DateTime.Parse(expected), Dates.FromFolders(rel));

    [Theory]
    [InlineData("DCIM/Camera/2021-12-24.jpg")]
    [InlineData("DCIM/Camera/x.jpg")]
    public void FolderNoDate(string rel) => Assert.Null(Dates.FromFolders(rel));

    [Fact] public void ExifOk() { Assert.True(Dates.ParseExif("2023:07:05 20:10:23", out var d)); Assert.Equal(new DateTime(2023, 7, 5, 20, 10, 23), d); }
    [Fact] public void ExifDateOnly() { Assert.True(Dates.ParseExif("2010:01:02", out var d)); Assert.Equal(new DateTime(2010, 1, 2), d); }
    [Theory]
    [InlineData("0000:00:00 00:00:00")]
    [InlineData("    :  :     :  :  ")]
    [InlineData("")]
    [InlineData("1899:12:31 00:00:00")]
    public void ExifRejected(string s) => Assert.False(Dates.ParseExif(s, out _));
}
