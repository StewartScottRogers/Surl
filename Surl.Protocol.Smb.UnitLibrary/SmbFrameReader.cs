using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smb;

/// <summary>
/// Reads NetBIOS session service frames (RFC 1002 section 4.3, used over direct TCP as
/// [MS-SMB] section 2.1 describes) from one connection, one after another.
/// </summary>
/// <remarks>
/// A frame is a type byte, a flags byte whose lowest bit extends the length to 17 bits, and a
/// 16-bit big-endian length, then that many bytes. A length over the message limit (ADR-0006:
/// <c>--max-message</c> bounds an SMB message; the limit counts the bytes after the NetBIOS
/// header) is never held in memory: the reader keeps the frame's first bytes, up to the 32-byte
/// SMB header, so the server can answer it, and reads and discards the rest, which the 17-bit
/// length bounds (ADR-0073, decision 7). It never reads a byte past the frame it is reading.
/// The flags byte's other bits are ignored. It is not safe for concurrent calls, and after
/// either closed outcome the caller stops reading.
/// </remarks>
internal sealed class SmbFrameReader
{
    /// <summary>The length of the NetBIOS session service header.</summary>
    public const int FrameHeaderLength = 4;

    /// <summary>The frame type of a session message, which carries an SMB message.</summary>
    public const byte SessionMessageType = 0x00;

    /// <summary>The frame type of a session keep-alive.</summary>
    public const byte SessionKeepAliveType = 0x85;

    private const int DiscardBufferLength = 8192;

    private readonly IConnection connection;
    private readonly long maxMessageBytes;
    private readonly byte[] frameHeader = new byte[FrameHeaderLength];

    /// <summary>
    /// Creates a reader over <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">The connection to read from.</param>
    /// <param name="maxMessageBytes">The most bytes a frame's body may hold; 0 means no limit beyond the 17-bit length.</param>
    public SmbFrameReader(IConnection connection, long maxMessageBytes)
    {
        this.connection = connection;
        this.maxMessageBytes = maxMessageBytes;
    }

    /// <summary>
    /// Reads the next frame.
    /// </summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The SMB message, or the named reason there is none.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async ValueTask<SmbFrameReadResult> ReadFrameAsync(CancellationToken cancellationToken)
    {
        var headerFilled = await FillAsync(frameHeader, cancellationToken);
        if (headerFilled < FrameHeaderLength)
        {
            return SmbFrameReadResult.NoMessage(headerFilled == 0 ? SmbFrameReadOutcome.ConnectionClosed : SmbFrameReadOutcome.ConnectionClosedMidFrame);
        }

        var frameType = frameHeader[0];
        var length = ((frameHeader[1] & 0x01) << 16) | (frameHeader[2] << 8) | frameHeader[3];
        if (maxMessageBytes > 0 && length > maxMessageBytes)
        {
            return await ReadFrameTooLargeAsync(frameType, length, cancellationToken);
        }

        var body = new byte[length];
        if (await FillAsync(body, cancellationToken) < length)
        {
            return SmbFrameReadResult.NoMessage(SmbFrameReadOutcome.ConnectionClosedMidFrame, frameType);
        }

        return frameType switch
        {
            SessionMessageType => new SmbFrameReadResult(SmbFrameReadOutcome.MessageRead, frameType, body, length),
            SessionKeepAliveType => SmbFrameReadResult.NoMessage(SmbFrameReadOutcome.KeepAlive, frameType),
            _ => SmbFrameReadResult.NoMessage(SmbFrameReadOutcome.UnexpectedFrameType, frameType),
        };
    }

    // Keeps the first bytes, up to the SMB header, and reads and discards the rest.
    private async ValueTask<SmbFrameReadResult> ReadFrameTooLargeAsync(byte frameType, int length, CancellationToken cancellationToken)
    {
        var start = new byte[Math.Min(length, SmbHeader.Length)];
        if (await FillAsync(start, cancellationToken) < start.Length
            || !await DiscardAsync(length - start.Length, cancellationToken))
        {
            return SmbFrameReadResult.NoMessage(SmbFrameReadOutcome.ConnectionClosedMidFrame, frameType);
        }

        return frameType == SessionMessageType
            ? new SmbFrameReadResult(SmbFrameReadOutcome.MessageTooLarge, frameType, start, length)
            : SmbFrameReadResult.NoMessage(SmbFrameReadOutcome.UnexpectedFrameType, frameType);
    }

    // Reads and drops count bytes; false when the client closed first.
    private async ValueTask<bool> DiscardAsync(int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[Math.Min(count, DiscardBufferLength)];
        while (count > 0)
        {
            var read = await FillAsync(buffer.AsMemory(0, Math.Min(count, buffer.Length)), cancellationToken);
            if (read == 0)
            {
                return false;
            }

            count -= read;
        }

        return true;
    }

    // Reads until buffer is full or the client closes; returns how many bytes were read.
    private async ValueTask<int> FillAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var filled = 0;
        while (filled < buffer.Length)
        {
            var read = await connection.ReadAsync(buffer[filled..], cancellationToken);
            if (read == 0)
            {
                break;
            }

            filled += read;
        }

        return filled;
    }
}
