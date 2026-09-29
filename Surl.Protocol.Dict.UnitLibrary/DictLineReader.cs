using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Dict;

/// <summary>
/// Reads DICT command lines from one connection, one after another (RFC 2229, section 2.2).
/// </summary>
/// <remarks>
/// A line ends at LF, with or without a CR before it; upstream curl always sends CRLF. Bytes
/// read past the end of a line stay buffered for the next line, so a client that sends
/// several commands at once, as upstream curl does, has them answered in order. A line is at
/// most the line limit in bytes, its line ending included (ADR-0006, section 1); while it looks
/// for the LF the reader never reads more than one byte past that limit. It is not safe for concurrent
/// calls, and after any outcome but <see cref="DictLineReadOutcome.LineRead"/> the caller
/// stops reading.
/// </remarks>
internal sealed class DictLineReader
{
    private const int InitialBufferBytes = 1024;

    private readonly IConnection connection;
    private readonly long maxLineBytes;
    private byte[] buffer = new byte[InitialBufferBytes];
    private int bufferedStart;
    private int bufferedEnd;
    private int scannedCount;

    /// <summary>
    /// Creates a reader over <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">The connection to read from.</param>
    /// <param name="maxLineBytes">The most bytes a line may hold, its line ending included; 0 means no limit.</param>
    public DictLineReader(IConnection connection, long maxLineBytes)
    {
        this.connection = connection;
        this.maxLineBytes = maxLineBytes;
    }

    private int BufferedCount => bufferedEnd - bufferedStart;

    /// <summary>
    /// Reads the next command line.
    /// </summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The line, or the named reason there is none.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async ValueTask<DictLineReadResult> ReadLineAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var lineFeed = Array.IndexOf(buffer, (byte)'\n', bufferedStart + scannedCount, BufferedCount - scannedCount);
            if (lineFeed >= 0)
            {
                return TakeLine(lineFeed + 1 - bufferedStart);
            }

            scannedCount = BufferedCount;
            if (IsOverLimit(BufferedCount))
            {
                return DictLineReadResult.NoLine(DictLineReadOutcome.LineTooLong);
            }

            if (!await FillAsync(cancellationToken))
            {
                return DictLineReadResult.NoLine(BufferedCount == 0
                    ? DictLineReadOutcome.ConnectionClosed
                    : DictLineReadOutcome.ConnectionClosedMidLine);
            }
        }
    }

    private DictLineReadResult TakeLine(int lineLength)
    {
        if (IsOverLimit(lineLength))
        {
            return DictLineReadResult.NoLine(DictLineReadOutcome.LineTooLong);
        }

        var content = buffer.AsSpan(bufferedStart, lineLength - 1);
        if (content.EndsWith("\r"u8))
        {
            content = content[..^1];
        }

        var line = Encoding.UTF8.GetString(content);
        bufferedStart += lineLength;
        scannedCount = 0;

        return DictLineReadResult.Read(line);
    }

    private bool IsOverLimit(long byteCount) => maxLineBytes > 0 && byteCount > maxLineBytes;

    private async ValueTask<bool> FillAsync(CancellationToken cancellationToken)
    {
        MakeRoomToRead();
        var room = buffer.Length - bufferedEnd;
        if (maxLineBytes > 0)
        {
            room = (int)Math.Min(room, maxLineBytes + 1 - BufferedCount);
        }

        var read = await connection.ReadAsync(buffer.AsMemory(bufferedEnd, room), cancellationToken);
        bufferedEnd += read;

        return read > 0;
    }

    private void MakeRoomToRead()
    {
        buffer.AsSpan(bufferedStart, BufferedCount).CopyTo(buffer);
        bufferedEnd = BufferedCount;
        bufferedStart = 0;

        if (bufferedEnd == buffer.Length)
        {
            Array.Resize(ref buffer, buffer.Length * 2);
        }
    }
}
