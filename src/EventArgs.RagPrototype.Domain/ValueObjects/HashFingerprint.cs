using System.Security.Cryptography;
using System.Text;

namespace EventArgs.RagPrototype.Domain.ValueObjects;

public static class HashFingerprint
{
    public static string ComputeSha256(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    public static string ComputeSha256(byte[] content)
    {
        var hash = SHA256.HashData(content);
        return Convert.ToHexStringLower(hash);
    }
}
