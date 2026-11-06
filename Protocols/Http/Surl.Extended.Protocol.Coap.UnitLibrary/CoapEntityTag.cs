using System.Buffers.Binary;
using System.Security.Cryptography;
using Surl.Content;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The ETag of a representation (RFC 7252 section 5.10.6; ADR-0097 decision 1): the first 8 bytes
/// of a SHA-256.
/// </summary>
internal static class CoapEntityTag
{
    private const int Length = 8;

    /// <summary>
    /// A file's ETag: over its length and its last-write time in UTC ticks, each as 8 big-endian
    /// bytes, so it changes whenever the file's status does and costs no read of the file.
    /// </summary>
    /// <param name="status">The file's status.</param>
    /// <returns>The 8-byte ETag.</returns>
    public static byte[] ForFile(ContentFileStatus status)
    {
        Span<byte> input = stackalloc byte[16];
        BinaryPrimitives.WriteInt64BigEndian(input, status.Length);
        BinaryPrimitives.WriteInt64BigEndian(input[8..], status.LastModifiedUtc.UtcTicks);

        return ForPayload(input);
    }

    /// <summary>A generated payload's ETag: over its bytes.</summary>
    /// <param name="payload">The payload.</param>
    /// <returns>The 8-byte ETag.</returns>
    public static byte[] ForPayload(ReadOnlySpan<byte> payload) => SHA256.HashData(payload)[..Length];
}
