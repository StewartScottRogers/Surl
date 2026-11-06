namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// An object's two digests, and the <c>x-goog-hash</c> value and ETag made from them (ADR-0110,
/// decision 1.1).
/// </summary>
/// <param name="Crc32c">The CRC-32C, four bytes, big-endian.</param>
/// <param name="Md5">The MD5, sixteen bytes.</param>
internal sealed record GcsObjectDigests(byte[] Crc32c, byte[] Md5)
{
    /// <summary>
    /// The <c>x-goog-hash</c> value: <c>crc32c=&lt;base64&gt;,md5=&lt;base64&gt;</c>.
    /// </summary>
    public string HashField => $"crc32c={Convert.ToBase64String(Crc32c)},md5={Convert.ToBase64String(Md5)}";

    /// <summary>
    /// The ETag: the quoted lower-case hexadecimal MD5 (ADR-0100, decision 2.3).
    /// </summary>
    public string ETag => $"\"{Convert.ToHexStringLower(Md5)}\"";
}
