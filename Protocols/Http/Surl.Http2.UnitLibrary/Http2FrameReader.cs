using Surl.Protocol.Abstractions;

namespace Surl.Http2;

/// <summary>
/// Reads HTTP/2 frames (RFC 9113 sections 4 and 6) from one connection, one after another, as
/// typed values.
/// </summary>
/// <remarks>
/// Reading is bounded: the 9-byte header is read first, and a frame longer than the
/// <c>SETTINGS_MAX_FRAME_SIZE</c> the server advertised is refused as
/// <see cref="Http2FrameMalformation.FrameTooLarge"/> without reading a byte of its payload
/// (section 4.2). A payload is otherwise allocated at its announced length, at most that
/// maximum. A frame of an unknown type is read through a small fixed buffer and discarded,
/// never held whole (section 5.5). The reader never reads a byte past the frame it is reading.
/// It is not safe for concurrent calls, and after a connection error or the peer closing the
/// caller stops reading; after a stream error the next frame can be read.
/// </remarks>
internal sealed class Http2FrameReader
{
    /// <summary>The <c>SETTINGS_MAX_FRAME_SIZE</c> RFC 9113 starts every connection with, and the least allowed.</summary>
    public const int DefaultMaxFrameSize = 16384;

    private const int SkipBufferLength = 4096;

    private readonly Func<Memory<byte>, CancellationToken, ValueTask<int>> readAsync;
    private readonly int maxFrameSize;
    private readonly byte[] headerBytes = new byte[Http2FrameHeader.EncodedLength];

    /// <summary>
    /// Creates a reader over <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">The connection to read from.</param>
    /// <param name="maxFrameSize">The <c>SETTINGS_MAX_FRAME_SIZE</c> the server advertised, 16384 to 16777215.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxFrameSize"/> is outside the range RFC 9113 allows.</exception>
    public Http2FrameReader(IConnection connection, int maxFrameSize)
        : this(connection.ReadAsync, maxFrameSize)
    {
    }

    /// <summary>
    /// Creates a reader over <paramref name="readAsync"/>, such as the read of the reader that
    /// recognised the connection preface, so bytes it already buffered are read first.
    /// </summary>
    /// <param name="readAsync">Reads at least one byte into a non-empty buffer, or returns 0 when the peer closed.</param>
    /// <param name="maxFrameSize">The <c>SETTINGS_MAX_FRAME_SIZE</c> the server advertised, 16384 to 16777215.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxFrameSize"/> is outside the range RFC 9113 allows.</exception>
    public Http2FrameReader(Func<Memory<byte>, CancellationToken, ValueTask<int>> readAsync, int maxFrameSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFrameSize, DefaultMaxFrameSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxFrameSize, Http2FrameHeader.MaxLength);
        this.readAsync = readAsync;
        this.maxFrameSize = maxFrameSize;
    }

    /// <summary>
    /// Reads the next frame.
    /// </summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The frame, the unknown frame skipped, the malformation, or the peer closing.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async ValueTask<Http2FrameReadResult> ReadFrameAsync(CancellationToken cancellationToken)
    {
        var filled = await FillAsync(headerBytes, cancellationToken);
        if (filled < Http2FrameHeader.EncodedLength)
        {
            return Http2FrameReadResult.Closed(
                filled == 0 ? Http2FrameReadOutcome.ConnectionClosed : Http2FrameReadOutcome.ConnectionClosedMidFrame);
        }

        var header = Http2FrameHeader.Read(headerBytes);
        if (header.Length > maxFrameSize)
        {
            return Http2FrameReadResult.Malformed(header, Http2FrameMalformation.FrameTooLarge);
        }

        return Http2FrameParser.IsDefinedType(header.Type)
            ? await ReadPayloadAsync(header, cancellationToken)
            : await SkipPayloadAsync(header, cancellationToken);
    }

    private async ValueTask<Http2FrameReadResult> ReadPayloadAsync(Http2FrameHeader header, CancellationToken cancellationToken)
    {
        var payload = new byte[header.Length];
        return await FillAsync(payload, cancellationToken) < payload.Length
            ? Http2FrameReadResult.Closed(Http2FrameReadOutcome.ConnectionClosedMidFrame)
            : Http2FrameParser.Parse(header, payload);
    }

    private async ValueTask<Http2FrameReadResult> SkipPayloadAsync(Http2FrameHeader header, CancellationToken cancellationToken)
    {
        var discard = new byte[Math.Min(header.Length, SkipBufferLength)];
        for (var remaining = header.Length; remaining > 0;)
        {
            var read = await readAsync(discard.AsMemory(0, Math.Min(remaining, discard.Length)), cancellationToken);
            if (read == 0)
            {
                return Http2FrameReadResult.Closed(Http2FrameReadOutcome.ConnectionClosedMidFrame);
            }

            remaining -= read;
        }

        return Http2FrameReadResult.UnknownSkipped(header);
    }

    private async ValueTask<int> FillAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var filled = 0;
        while (filled < buffer.Length)
        {
            var read = await readAsync(buffer[filled..], cancellationToken);
            if (read == 0)
            {
                break;
            }

            filled += read;
        }

        return filled;
    }
}
