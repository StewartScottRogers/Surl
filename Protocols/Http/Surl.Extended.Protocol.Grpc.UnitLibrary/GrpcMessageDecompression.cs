using System.IO.Compression;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// Reads a call's <c>grpc-encoding</c> and turns each Length-Prefixed-Message into the serialized message it
/// carries, decompressing it when its Compressed-Flag is set (ADR-0116 decisions 6 and 8).
/// </summary>
public static class GrpcMessageDecompression
{
    /// <summary>
    /// The <c>grpc-accept-encoding</c> value every response carries: the encodings the server reads.
    /// </summary>
    public const string AcceptEncodingValue = "identity,deflate,gzip";

    /// <summary>
    /// Reads the call's <c>grpc-encoding</c>, compared case-insensitively.
    /// </summary>
    /// <param name="grpcEncoding">The header value, or <see langword="null" /> when the call sent none.</param>
    /// <returns>The encoding; <see cref="GrpcMessageEncoding.Identity" /> when the call sent none.</returns>
    /// <exception cref="GrpcCallException"><see cref="GrpcStatus.GrpcEncodingNotServed" /> for any other encoding.</exception>
    public static GrpcMessageEncoding ReadEncoding(string? grpcEncoding) => grpcEncoding?.ToLowerInvariant() switch
    {
        null or "identity" => GrpcMessageEncoding.Identity,
        "gzip" => GrpcMessageEncoding.Gzip,
        "deflate" => GrpcMessageEncoding.Deflate,
        _ => throw new GrpcCallException(GrpcStatus.GrpcEncodingNotServed),
    };

    /// <summary>
    /// The serialized message <paramref name="message" /> carries. A message with Compressed-Flag 0 is read as is
    /// under any encoding.
    /// </summary>
    /// <param name="message">The message as framed.</param>
    /// <param name="encoding">The call's encoding.</param>
    /// <param name="maximumMessageLength">The longest the decompressed message may be (<c>--max-message</c>).</param>
    /// <returns>The serialized message.</returns>
    /// <exception cref="GrpcCallException">
    /// <see cref="GrpcStatus.CompressedFlagSetWithoutGrpcEncoding" /> for a compressed message under identity;
    /// <see cref="GrpcStatus.MessageDidNotDecompress" /> for one that does not decompress;
    /// <see cref="GrpcStatus.MessageLargerThanTheLimit" /> for one longer than the limit once decompressed.
    /// </exception>
    public static ReadOnlyMemory<byte> ReadPayload(LengthPrefixedMessage message, GrpcMessageEncoding encoding, int maximumMessageLength)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!message.IsCompressed)
        {
            return message.Payload;
        }

        if (encoding == GrpcMessageEncoding.Identity)
        {
            throw new GrpcCallException(GrpcStatus.CompressedFlagSetWithoutGrpcEncoding);
        }

        return Decompress(message.Payload, encoding, maximumMessageLength);
    }

    private static byte[] Decompress(ReadOnlyMemory<byte> payload, GrpcMessageEncoding encoding, int maximumMessageLength)
    {
        using var compressed = new MemoryStream(payload.ToArray(), writable: false);
        using Stream decompressing = encoding == GrpcMessageEncoding.Gzip
            ? new GZipStream(compressed, CompressionMode.Decompress)
            : new ZLibStream(compressed, CompressionMode.Decompress);
        var decompressed = new MemoryStream();
        var buffer = new byte[(int)Math.Min(maximumMessageLength + 1L, 81920)];
        int read;
        while ((read = ReadOnce(decompressing, buffer)) > 0)
        {
            decompressed.Write(buffer, 0, read);
            if (decompressed.Length > maximumMessageLength)
            {
                throw new GrpcCallException(GrpcStatus.MessageLargerThanTheLimit);
            }
        }

        return decompressed.ToArray();
    }

    private static int ReadOnce(Stream decompressing, byte[] buffer)
    {
        try
        {
            return decompressing.Read(buffer, 0, buffer.Length);
        }
        catch (InvalidDataException failure)
        {
            throw new GrpcCallException(GrpcStatus.MessageDidNotDecompress, failure);
        }
    }
}
