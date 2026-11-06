using System.Text;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// What the server checks of an object key before the store sees it ("Object key naming
/// guidelines"; ADR-0108, decision 3).
/// </summary>
internal static class S3ObjectKey
{
    /// <summary>
    /// The most UTF-8 bytes a key may have.
    /// </summary>
    public const int MaxKeyBytes = 1024;

    /// <summary>
    /// Whether a key, percent-decoded once as UTF-8, is longer than <see cref="MaxKeyBytes"/> bytes.
    /// </summary>
    /// <param name="rawKey">The key as the client sent it, still percent-encoded.</param>
    /// <returns><see langword="true"/> when the key is too long.</returns>
    public static bool IsTooLong(string rawKey) =>
        Encoding.UTF8.GetByteCount(Uri.UnescapeDataString(rawKey)) > MaxKeyBytes;
}
