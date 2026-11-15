using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Draws an upload session's UUID from an identifier source: a version 4 UUID in form (version and
/// RFC 4122 variant bits set), its other 122 bits seedable, so a seeded instance repeats it (ADR-0163).
/// </summary>
internal static class OciSessionUuid
{
    private const int ByteCount = 16;
    private const int VersionByte = 6;
    private const int VariantByte = 8;

    /// <summary>
    /// Draws a UUID.
    /// </summary>
    /// <param name="identifiers">Where the UUID's bits come from.</param>
    /// <returns>A version 4, RFC 4122 variant UUID.</returns>
    public static Guid Draw(IIdentifierRandom identifiers)
    {
        Span<byte> bytes = stackalloc byte[ByteCount];
        identifiers.NextBytes(bytes);
        bytes[VersionByte] = (byte)((bytes[VersionByte] & 0x0F) | 0x40);
        bytes[VariantByte] = (byte)((bytes[VariantByte] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }
}
