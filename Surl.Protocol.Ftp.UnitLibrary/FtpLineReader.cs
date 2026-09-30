using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ftp;

/// <summary>
/// Reads FTP command lines from one control connection, one after another (RFC 959, section
/// 4.1; ADR-0052, decision 10).
/// </summary>
/// <remarks>
/// <para>
/// A line ends at LF; a CR just before it is dropped, and a bare CR inside a line is kept as a
/// byte of the line. Upstream curl always sends CRLF. Bytes
/// read past the end of a line stay buffered for the next line, so a client that sends
/// several commands at once, as upstream curl does, has them answered in order. A line is at
/// most the line limit in bytes, its line ending included (ADR-0006, section 1); while it looks
/// for the LF the reader never reads more than one byte past that limit.
/// </para>
/// <para>
/// The head timeout (ADR-0006, section 1) bounds how long one line may take to arrive. Its
/// clock starts at <see cref="StartHeadTimeout"/>, or else at the first byte of the line: when
/// a read returns it, or, for a byte already buffered behind the previous line, when
/// <see cref="ReadLineAsync"/> is called. Until then a read waits with no clock but the
/// caller's cancellation token. The clock stops when the line is complete.
/// </para>
/// <para>
/// It is not safe for concurrent calls, and after any outcome but
/// <see cref="FtpLineReadOutcome.LineRead"/> the caller stops reading.
/// </para>
/// </remarks>
internal sealed class FtpLineReader : IDisposable
{
    private const int InitialBufferBytes = 1024;

    private readonly IConnection connection;
    private readonly long maxLineBytes;
    private readonly TimeSpan headTimeout;
    private readonly TimeProvider timeProvider;
    private byte[] buffer = new byte[InitialBufferBytes];
    private int bufferedStart;
    private int bufferedEnd;
    private int scannedCount;
    private CancellationTokenSource? headTimeoutClock;

    /// <summary>
    /// Creates a reader over <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">The connection to read from.</param>
    /// <param name="maxLineBytes">The most bytes a line may hold, its line ending included; 0 means no limit.</param>
    /// <param name="headTimeout">How long one line may take to arrive; <see cref="Timeout.InfiniteTimeSpan"/> means no limit.</param>
    /// <param name="timeProvider">The clock the head timeout runs on.</param>
    public FtpLineReader(IConnection connection, long maxLineBytes, TimeSpan headTimeout, TimeProvider timeProvider)
    {
        this.connection = connection;
        this.maxLineBytes = maxLineBytes;
        this.headTimeout = headTimeout;
        this.timeProvider = timeProvider;
    }

    private int BufferedCount => bufferedEnd - bufferedStart;

    /// <summary>
    /// Starts the head timeout's clock for the next line now, if it is not already running.
    /// </summary>
    public void StartHeadTimeout()
    {
        if (headTimeoutClock is null && headTimeout != Timeout.InfiniteTimeSpan)
        {
            headTimeoutClock = new CancellationTokenSource(headTimeout, timeProvider);
        }
    }

    /// <summary>
    /// Reads the next command line.
    /// </summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The line, or the named reason there is none.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async ValueTask<FtpLineReadResult> ReadLineAsync(CancellationToken cancellationToken)
    {
        if (BufferedCount > 0)
        {
            StartHeadTimeout();
        }

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
                return FtpLineReadResult.NoLine(FtpLineReadOutcome.LineTooLong);
            }

            if (await FillAsync(cancellationToken) is { } outcome)
            {
                return FtpLineReadResult.NoLine(outcome);
            }
        }
    }

    /// <summary>
    /// Stops the head timeout's clock.
    /// </summary>
    public void Dispose() => StopHeadTimeout();

    private FtpLineReadResult TakeLine(int lineLength)
    {
        if (IsOverLimit(lineLength))
        {
            return FtpLineReadResult.NoLine(FtpLineReadOutcome.LineTooLong);
        }

        var content = buffer.AsSpan(bufferedStart, lineLength - 1);
        if (content.EndsWith("\r"u8))
        {
            content = content[..^1];
        }

        var line = content.ToArray();
        bufferedStart += lineLength;
        scannedCount = 0;
        StopHeadTimeout();

        return FtpLineReadResult.Read(line);
    }

    private void StopHeadTimeout()
    {
        headTimeoutClock?.Dispose();
        headTimeoutClock = null;
    }

    private bool IsOverLimit(long byteCount) => maxLineBytes > 0 && byteCount > maxLineBytes;

    // Reads more bytes; returns why no more will come, or null when some arrived.
    private async ValueTask<FtpLineReadOutcome?> FillAsync(CancellationToken cancellationToken)
    {
        MakeRoomToRead();
        var room = buffer.Length - bufferedEnd;
        if (maxLineBytes > 0)
        {
            room = (int)Math.Min(room, maxLineBytes + 1 - BufferedCount);
        }

        int read;
        try
        {
            read = await ReadWithinHeadTimeoutAsync(buffer.AsMemory(bufferedEnd, room), cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return FtpLineReadOutcome.HeadTimedOut;
        }

        if (read == 0)
        {
            return BufferedCount == 0 ? FtpLineReadOutcome.ConnectionClosed : FtpLineReadOutcome.ConnectionClosedMidLine;
        }

        bufferedEnd += read;
        StartHeadTimeout();

        return null;
    }

    private async ValueTask<int> ReadWithinHeadTimeoutAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        if (headTimeoutClock is null)
        {
            return await connection.ReadAsync(destination, cancellationToken);
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, headTimeoutClock.Token);

        return await connection.ReadAsync(destination, cancellation.Token);
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
