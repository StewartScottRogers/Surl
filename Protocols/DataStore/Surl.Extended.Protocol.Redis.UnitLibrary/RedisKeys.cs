using System.Text;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Keys as the keyspace holds them: one character per byte (Latin-1), so every byte string is a
/// distinct key and comparing, hashing and glob matching run on the bytes.
/// </summary>
internal static class RedisKeys
{
    /// <summary>The key <paramref name="bytes"/> names.</summary>
    public static string Name(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    /// <summary>The bytes of <paramref name="key"/>.</summary>
    public static byte[] Bytes(string key) => Encoding.Latin1.GetBytes(key);
}
