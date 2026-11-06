using System.Buffers.Binary;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// One PROTOCOL-HTTP2.md Length-Prefixed-Message: a 1-byte Compressed-Flag, a 4-byte big-endian
/// Message-Length, and the message.
/// </summary>
/// <param name="IsCompressed">Whether the Compressed-Flag is 1: the message is compressed with the call's <c>grpc-encoding</c>.</param>
/// <param name="Payload">The message as it travelled, compressed or not.</param>
public sealed record LengthPrefixedMessage(bool IsCompressed, ReadOnlyMemory<byte> Payload)
{
    /// <summary>The length of the Compressed-Flag and Message-Length before every message.</summary>
    public const int PrefixLength = 5;

    /// <summary>
    /// Writes an uncompressed message, as every reply is written (ADR-0116 decision 6).
    /// </summary>
    /// <param name="payload">The serialized message.</param>
    /// <returns>The prefix, then the message.</returns>
    public static byte[] WriteUncompressed(ReadOnlySpan<byte> payload)
    {
        var framed = new byte[PrefixLength + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(framed.AsSpan(1), (uint)payload.Length);
        payload.CopyTo(framed.AsSpan(PrefixLength));
        return framed;
    }
}
