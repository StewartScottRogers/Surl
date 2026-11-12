using System.Buffers.Binary;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// An object's two digests, and the <c>x-goog-hash</c> value and ETag made from them (ADR-0110,
/// decision 1.1).
/// </summary>
/// <param name="Crc32c">The CRC-32C, four bytes, big-endian.</param>
/// <param name="Md5">The MD5, sixteen bytes.</param>
internal sealed record GcsObjectDigests(byte[] Crc32c, byte[] Md5)
{
    private const int Md5HexLength = 32;

    /// <summary>
    /// The <c>x-goog-hash</c> value: <c>crc32c=&lt;base64&gt;,md5=&lt;base64&gt;</c>.
    /// </summary>
    public string HashField => $"crc32c={Convert.ToBase64String(Crc32c)},md5={Convert.ToBase64String(Md5)}";

    /// <summary>
    /// The ETag: the quoted lower-case hexadecimal MD5 (ADR-0100, decision 2.3).
    /// </summary>
    public string ETag => $"\"{Convert.ToHexStringLower(Md5)}\"";

    /// <summary>
    /// The CRC-32C as a number, the form the object metadata index caches.
    /// </summary>
    public uint Crc32cValue => BinaryPrimitives.ReadUInt32BigEndian(Crc32c);

    /// <summary>
    /// The digests an object's cached properties name, without reading the file: the cached CRC-32C
    /// and the MD5 its ETag quotes.
    /// </summary>
    /// <param name="eTag">The object's ETag.</param>
    /// <param name="crc32c">The object's cached CRC-32C, or <see langword="null"/>.</param>
    /// <returns>The digests, or <see langword="null"/> when there is no cached CRC-32C or the ETag
    /// is not a quoted 32-digit hexadecimal MD5 (a multipart upload's <c>"&lt;hex&gt;-&lt;parts&gt;"</c>
    /// is not), so the file must be read.</returns>
    public static GcsObjectDigests? Cached(string eTag, uint? crc32c)
    {
        if (crc32c is null
            || eTag.Length != Md5HexLength + 2
            || eTag[0] != '"'
            || eTag[^1] != '"'
            || !IsHex(eTag.AsSpan(1, Md5HexLength)))
        {
            return null;
        }

        var crcBytes = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc32c.Value);

        return new GcsObjectDigests(crcBytes, Convert.FromHexString(eTag.AsSpan(1, Md5HexLength)));
    }

    private static bool IsHex(ReadOnlySpan<char> text) => !text.ContainsAnyExcept("0123456789abcdefABCDEF");
}
