using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Draws the <c>UploadId</c> of a multipart upload: 32 lower-case hex digits from an identifier source,
/// so a seeded instance hands out the same ID for the same exchange order (ADR-0163, ADR-0124 decision 6).
/// </summary>
internal static class S3UploadId
{
    private const int ByteCount = 16;

    /// <summary>
    /// Draws an upload ID.
    /// </summary>
    /// <param name="identifiers">Where the ID's bytes come from.</param>
    /// <returns>32 lower-case hex digits.</returns>
    public static string Draw(IIdentifierRandom identifiers)
    {
        Span<byte> bytes = stackalloc byte[ByteCount];
        identifiers.NextBytes(bytes);
        return Convert.ToHexStringLower(bytes);
    }
}
