using System.Security.Cryptography;

// SHA-256 identity of file content.
// The same bytes always produce the same lowercase hexadecimal fingerprint.
// A path is not part of the hash: the same content at two paths is one fingerprint.
public static class ContentFingerprint
{
    public const string Prefix = "sha256:";

    public static string FromBytes(ReadOnlySpan<byte> content)
    {
        var hash = SHA256.HashData(content);
        return Prefix + Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return FromBytes(File.ReadAllBytes(path));
    }
}
