using Surl.Hpack;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// Writes the server's HTTP/2 frames to one connection, one whole frame at a time, so frames
/// the connection's reader and its streams' writers send at once never interleave (RFC 9113
/// section 4). It owns the connection's HPACK encoder and encodes each response header block
/// while it holds the connection, so blocks reach the client in the order the encoder's dynamic
/// table assumes (RFC 9113 section 4.3).
/// </summary>
/// <param name="connection">The connection frames are written to.</param>
internal sealed class Http2FrameWriter(IConnection connection)
{
    /// <summary>
    /// The longest payload the server sends: RFC 9113's initial <c>SETTINGS_MAX_FRAME_SIZE</c>,
    /// the least a client may advertise, so every client accepts it (ADR-0081, decision 6).
    /// </summary>
    public const int MaxPayloadLength = Http2FrameReader.DefaultMaxFrameSize;

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly HpackEncoder encoder = new();

    /// <summary>
    /// Writes one frame.
    /// </summary>
    /// <param name="frame">The frame, its payload at most <see cref="MaxPayloadLength"/> bytes.</param>
    /// <param name="cancellationToken">Cuts the write off; the connection is then unusable.</param>
    /// <returns>A task that completes when the frame is written.</returns>
    public async Task WriteAsync(Http2Frame frame, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await connection.WriteAsync(Http2FrameEncoder.Encode(frame), cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Encodes <paramref name="fields"/> as one header block and writes it on
    /// <paramref name="streamId"/>: one <c>HEADERS</c> frame, followed by <c>CONTINUATION</c>s
    /// when the block is longer than <see cref="MaxPayloadLength"/>, with nothing between them.
    /// </summary>
    /// <param name="streamId">The stream.</param>
    /// <param name="fields">The fields, pseudo-header fields first.</param>
    /// <param name="endStream">Whether the <c>HEADERS</c> frame ends the stream.</param>
    /// <param name="cancellationToken">Cuts the write off; the connection is then unusable.</param>
    /// <returns>A task that completes when the block is written.</returns>
    public async Task WriteHeaderBlockAsync(int streamId, IEnumerable<HpackHeaderField> fields, bool endStream, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await connection.WriteAsync(HeaderBlockFrames(streamId, encoder.Encode(fields), endStream), cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Applies the client's <c>SETTINGS_HEADER_TABLE_SIZE</c> to the encoder before the next block.
    /// </summary>
    /// <param name="headerTableSize">The size the client's decoder allows.</param>
    /// <param name="cancellationToken">Cuts the wait for the connection off.</param>
    /// <returns>A task that completes when the size is applied.</returns>
    public async Task SetClientHeaderTableSizeAsync(uint headerTableSize, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            encoder.SetPeerMaximumTableSize((int)Math.Min(headerTableSize, int.MaxValue));
        }
        finally
        {
            gate.Release();
        }
    }

    private static byte[] HeaderBlockFrames(int streamId, byte[] block, bool endStream)
    {
        using var frames = new MemoryStream();
        var length = Math.Min(block.Length, MaxPayloadLength);
        frames.Write(Http2FrameEncoder.Encode(new Http2HeadersFrame(streamId, block[..length], endStream, length == block.Length)));
        for (var offset = length; offset < block.Length; offset += length)
        {
            length = Math.Min(block.Length - offset, MaxPayloadLength);
            var endHeaders = offset + length == block.Length;
            frames.Write(Http2FrameEncoder.Encode(new Http2ContinuationFrame(streamId, block[offset..(offset + length)], endHeaders)));
        }

        return frames.ToArray();
    }
}
