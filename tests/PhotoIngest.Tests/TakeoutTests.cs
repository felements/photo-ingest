using PhotoIngest;

public class TakeoutTests
{
    [Fact]
    public void ParsesTitleAndStrippedName()
    {
        var map = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        Takeout.AddJson("Photos from 2023/IMG_1.jpg.json", """{"title":"IMG_1.jpg","photoTakenTime":{"timestamp":"1700000000","formatted":"x"}}""", map);
        Takeout.AddJson("Photos from 2023/IMG_2.jpg.supplemental-metadata.json", """{"title":"IMG_2 original name.jpg","photoTakenTime":{"timestamp":"1700000000"}}""", map);
        Takeout.AddJson("user-generated-memory-titles.json", """{"title":["a","b"]}""", map);
        Takeout.AddJson("broken.json", "{not json", map);
        var expected = DateTimeOffset.FromUnixTimeSeconds(1700000000).ToLocalTime().DateTime;
        Assert.Equal(expected, map["Photos from 2023/IMG_1.jpg"]);
        Assert.Equal(expected, map["Photos from 2023/IMG_2.jpg"]);
        Assert.Equal(expected, map["Photos from 2023/IMG_2 original name.jpg"]);
        Assert.Equal(3, map.Count);
    }

    [Fact]
    public void BuildReadsFromDisk()
    {
        var root = Path.Combine(Path.GetTempPath(), "takeout-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "d"));
        File.WriteAllText(Path.Combine(root, "d", "a.jpg.json"), """{"title":"a.jpg","photoTakenTime":{"timestamp":"1600000000"}}""");
        try
        {
            var map = Takeout.Build(root, new[] { "d/a.jpg.json", "d/missing.json" });
            Assert.Single(map);
            Assert.True(map.ContainsKey("d/a.jpg"));
        }
        finally { Directory.Delete(root, true); }
    }
}
