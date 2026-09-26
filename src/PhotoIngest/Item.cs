namespace PhotoIngest;

public sealed class Item
{
    public string Rel = "";
    public string Full = "";
    public long Size;
    public DateTime Mtime;
    public Bucket Bucket;
    public string Reason = "";
    public string Ext = "";
    public string Stem = "";
    public string? Sha;
}
