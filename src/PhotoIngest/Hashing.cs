using System.Security.Cryptography;

namespace PhotoIngest;

public static class Hashing
{
    public static string Sha256(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
        return Convert.ToHexStringLower(SHA256.HashData(fs));
    }
}
