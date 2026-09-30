using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smb;

/// <summary>
/// Reads NetBIOS session service frames (RFC 1002 section 4.3, used over direct TCP as
/// [MS-SMB] section 2.1 describes) from one connection, one after another.
/// </summary>
/// <remarks>
/// A frame is a type byte, a flags byte whose lowest bit extends the length to 17 bits, and a
/// 16-bit big-endian length, then that many bytes. The reader reads the 4-byte header, refuses
/// a length over the message limit before reading any of the body (ADR-0006: <c>--max-message</c>
/// bounds an SMB message; the limit counts the bytes after the NetBIOS header), and never reads
/// a byte past the frame it is reading. The flags byte's other bits are ignored. It is not safe
/// for concurrent calls, and after <see cref="SmbFrameReadOutcome.MessageTooLarge"/> or either
/// closed outcome the caller stops reading.
/// </remarks>
internal sealed class SmbFrameReader
{
    /// <summary>The length of the NetBIOS session service header.</summary>
    public const int FrameHeaderLength = 4;

    /// <summary>The frame type of a session message, which carries an SMB message.</summary>
    public const byte SessionMessageType = 0x00;

    /// <summary>The frame type of a session keep-alive.</summary>
    public const byte SessionKeepAliveType = 0x85;

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
            return SmbFrameReadResult.NoMessage(SmbFrameReadOutcome.MessageTooLarge, frameType);
        }

        var body = new byte[length];
        if (await FillAsync(body, cancellationToken) < length)
        {
            return SmbFrameReadResult.NoMessage(SmbFrameReadOutcome.ConnectionClosedMidFrame, frameType);
        }

        return frameType switch
        {
            SessionMessageType => new SmbFrameReadResult(SmbFrameReadOutcome.MessageRead, frameType, body),
            SessionKeepAliveType => SmbFrameReadResult.NoMessage(SmbFrameReadOutcome.KeepAlive, frameType),
            _ => SmbFrameReadResult.NoMessage(SmbFrameReadOutcome.UnexpectedFrameType, frameType),
        };
    }

    // Reads until buffer is full or the client closes; returns how many bytes were read.
    private async ValueTask<int> FillAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        var filled = 0;
        while (filled < buffer.Length)
        {
            var read = await connection.ReadAsync(buffer.AsMemory(filled), cancellationToken);
            if (read == 0)
            {
                break;
            }

            filled += read;
        }

        return filled;
    }
}
