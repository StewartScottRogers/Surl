using System.IO.Compression;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Inflates the client's packet payloads as one zlib stream (RFC 1950) that runs from the
/// moment compression starts until the next <c>NEWKEYS</c> (RFC 4253, section 6.2), each
/// payload counted against a limit and stopped the moment it passes it (ADR-0051, decision 9).
/// </summary>
internal sealed class SshZlibDecompressor : IDisposable
{
    private const int ChunkBytes = 8192;

    // The current packet's compressed bytes, which the inflater reads to their end.
    private readonly MemoryStream compressed = new();
    private readonly byte[] chunk = new byte[ChunkBytes];
    private readonly ZLibStream inflater;
    private readonly long maxPayloadBytes;

    /// <summary>
    /// Starts a new zlib stream.
    /// </summary>
    /// <param name="maxPayloadBytes">The most bytes one payload may inflate to.</param>
    public SshZlibDecompressor(long maxPayloadBytes)
    {
        this.maxPayloadBytes = maxPayloadBytes;
        inflater = new ZLibStream(compressed, CompressionMode.Decompress, leaveOpen: true);
    }

    /// <summary>
    /// Inflates <paramref name="payload"/> as the stream's next piece. At most one byte past the
    /// limit is ever inflated.
    /// </summary>
    /// <param name="payload">The packet's compressed payload.</param>
    /// <returns>The message, message number first.</returns>
    /// <exception cref="SshDisconnectRequiredException">
    /// The payload does not inflate, inflates to nothing, or inflates past the limit:
    /// <c>DISCONNECT</c> 6.
    /// </exception>
    public byte[] Decompress(byte[] payload)
    {
        compressed.SetLength(0);
        compressed.Write(payload);
        compressed.Position = 0;
        using var message = new MemoryStream();
        try
        {
            InflateInto(message);
        }
        catch (InvalidDataException)
        {
            throw CompressionError($"An SSH packet's compressed payload of {payload.Length} bytes does not decompress.");
        }

        return message.Length > 0
            ? message.ToArray()
            : throw CompressionError($"An SSH packet's compressed payload of {payload.Length} bytes decompresses to no message.");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        inflater.Dispose();
        compressed.Dispose();
    }

    private static SshDisconnectRequiredException CompressionError(string note) =>
        new(SshDisconnectReason.CompressionError, "Compression error", note);

    private void InflateInto(MemoryStream message)
    {
        int read;
        while ((read = inflater.Read(chunk, 0, (int)Math.Min(ChunkBytes, maxPayloadBytes - message.Length + 1))) > 0)
        {
            message.Write(chunk, 0, read);
            if (message.Length > maxPayloadBytes)
            {
                throw CompressionError($"An SSH packet's compressed payload decompresses past the {maxPayloadBytes}-byte message limit.");
            }
        }
    }
}
