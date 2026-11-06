using Surl.Protocol.Abstractions;

namespace Surl.LineProtocol;

/// <summary>
/// Reads CRLF-ended command lines, counted runs, dot-stuffed bodies and SASL continuations from
/// one connection through one buffer (ADR-0050, decision 8), for the SMTP, IMAP and POP3 servers.
/// </summary>
/// <remarks>
/// <para>
/// A line ends only at CRLF; a bare LF or a bare CR is part of the line (RFC 5321, section
/// 2.3.8). A line is at most <see cref="ExchangeLimits.MaxLineBytes"/> bytes, its CRLF included
/// (ADR-0006, section 1), and the buffer is never filled past that limit while a line is read,
/// so no byte past it is read. Bytes after a line stay buffered for the next read, so pipelined
/// commands are answered in order.
/// </para>
/// <para>
/// The head timeout (<see cref="ExchangeLimits.HeadTimeout"/>) bounds how long one line may take
/// to arrive. For the first line its clock starts when the reader is created, which a server does
/// at the start of its exchange; for every later line it starts at the line's first byte, when a
/// read returns it or, for a byte already buffered, when the line is asked for. It stops when the
/// line is complete. Counted runs and bodies are not under the head timeout; the engine's idle
/// timeout and maximum duration cancel them through the caller's token.
/// </para>
/// <para>
/// Outcomes are values, not exceptions: the server answers each in its own words (ADR-0006,
/// section 5). It is not safe for concurrent calls, and after any outcome but a successful one
/// the caller stops reading.
/// </para>
/// </remarks>
public sealed class CrlfLineReader : IDisposable
{
    private const int InitialBufferBytes = 1024;

    private readonly IConnection connection;
    private readonly long maxLineBytes;
    private readonly long maxUploadBytes;
    private readonly TimeSpan headTimeout;
    private readonly TimeProvider timeProvider;
    private byte[] buffer = new byte[InitialBufferBytes];
    private int bufferedStart;
    private int bufferedEnd;
    private int scannedCount;
    private CancellationTokenSource? headTimeoutClock;

    /// <summary>
    /// Creates a reader over <paramref name="connection"/> and starts the first line's head timeout.
    /// </summary>
    /// <param name="connection">The connection to read from.</param>
    /// <param name="limits">The exchange's limits: the line limit, the head timeout and the upload limit; a size of 0 means no limit.</param>
    /// <param name="timeProvider">The clock the head timeout runs on.</param>
    public CrlfLineReader(IConnection connection, ExchangeLimits limits, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.connection = connection;
        maxLineBytes = limits.MaxLineBytes;
        maxUploadBytes = limits.MaxUploadBytes;
        headTimeout = limits.HeadTimeout;
        this.timeProvider = timeProvider;
        StartHeadTimeout();
    }

    private int BufferedCount => bufferedEnd - bufferedStart;

    /// <summary>
    /// Reads the next CRLF-ended line.
    /// </summary>
    /// <param name="cancellationToken">The exchange's cancellation; cuts the read off.</param>
    /// <returns>The line without its CRLF, or the named reason there is none.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async ValueTask<CrlfLineReadResult> ReadLineAsync(CancellationToken cancellationToken)
    {
        if (BufferedCount > 0)
        {
            StartHeadTimeout();
        }

        while (true)
        {
            var lineLength = FindLineLength();
            if (lineLength > 0)
            {
                return CrlfLineReadResult.Read(TakeLine(lineLength));
            }

            if (maxLineBytes > 0 && BufferedCount >= maxLineBytes)
            {
                return CrlfLineReadResult.NoLine(CrlfLineReadOutcome.LineTooLong);
            }

            if (await FillLineAsync(cancellationToken) is { } outcome)
            {
                return CrlfLineReadResult.NoLine(outcome);
            }
        }
    }

    /// <summary>
    /// Reads one SASL continuation line with <see cref="ReadLineAsync"/>'s bounds and outcomes,
    /// and classifies it with <see cref="SaslContinuationLine.Classify"/>.
    /// </summary>
    /// <param name="cancellationToken">The exchange's cancellation; cuts the read off.</param>
    /// <returns>The decoded response, or the named reason there is none.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async ValueTask<SaslContinuationReadResult> ReadSaslContinuationAsync(CancellationToken cancellationToken)
    {
        var line = await ReadLineAsync(cancellationToken);

        return line.Outcome switch
        {
            CrlfLineReadOutcome.LineRead => SaslContinuationLine.Classify(line.Line),
            CrlfLineReadOutcome.Closed => SaslContinuationReadResult.NoResponse(SaslContinuationOutcome.Closed),
            CrlfLineReadOutcome.LineTooLong => SaslContinuationReadResult.NoResponse(SaslContinuationOutcome.LineTooLong),
            _ => SaslContinuationReadResult.NoResponse(SaslContinuationOutcome.HeadTimedOut),
        };
    }

    /// <summary>
    /// Copies the next <paramref name="byteCount"/> bytes, buffered first, to
    /// <paramref name="destination"/>: an IMAP literal (<c>{n}</c>), bounded by the caller.
    /// </summary>
    /// <param name="byteCount">How many bytes to copy.</param>
    /// <param name="destination">Where the bytes go.</param>
    /// <param name="cancellationToken">The exchange's cancellation; cuts the read off.</param>
    /// <returns><see langword="true"/> when every byte was copied; <see langword="false"/> when the peer closed first.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async ValueTask<bool> ReadCountedRunAsync(long byteCount, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteCount);
        ArgumentNullException.ThrowIfNull(destination);

        var remaining = byteCount;
        while (remaining > 0)
        {
            if (BufferedCount == 0 && await ReadMoreAsync(int.MaxValue, underHeadTimeout: false, cancellationToken) == 0)
            {
                return false;
            }

            var taken = (int)Math.Min(remaining, BufferedCount);
            await destination.WriteAsync(buffer.AsMemory(bufferedStart, taken), cancellationToken);
            Consume(taken);
            remaining -= taken;
        }

        return true;
    }

    /// <summary>
    /// Reads a dot-stuffed body up to CRLF <c>.</c> CRLF (RFC 5321, section 4.5.2), unstuffed,
    /// into <paramref name="destination"/>, bounded by <see cref="ExchangeLimits.MaxUploadBytes"/>.
    /// </summary>
    /// <remarks>
    /// The body's first line counts as following a CRLF. The body's final CRLF is written; the
    /// terminator's <c>.</c> CRLF is not. A bare LF or a bare CR never ends the body. Bytes after
    /// the terminator stay buffered for the next line. Past the upload limit nothing more is read.
    /// </remarks>
    /// <param name="destination">Where the unstuffed body goes.</param>
    /// <param name="cancellationToken">The exchange's cancellation; cuts the read off.</param>
    /// <returns>How the read ended and how many bytes were written.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async ValueTask<DotStuffedBodyReadResult> ReadDotStuffedBodyAsync(Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var unstuffer = new DotUnstuffer();
        long written = 0;
        while (true)
        {
            if (BufferedCount == 0 && await ReadMoreAsync(int.MaxValue, underHeadTimeout: false, cancellationToken) == 0)
            {
                return new DotStuffedBodyReadResult(DotStuffedBodyReadOutcome.Closed, written);
            }

            var unstuffed = new byte[BufferedCount + 1];
            var step = unstuffer.Unstuff(buffer.AsSpan(bufferedStart, BufferedCount), unstuffed);
            Consume(step.Consumed);
            if (maxUploadBytes > 0 && written + step.Written > maxUploadBytes)
            {
                return new DotStuffedBodyReadResult(DotStuffedBodyReadOutcome.BodyTooLarge, written);
            }

            await destination.WriteAsync(unstuffed.AsMemory(0, step.Written), cancellationToken);
            written += step.Written;
            if (step.Ended)
            {
                return new DotStuffedBodyReadResult(DotStuffedBodyReadOutcome.BodyRead, written);
            }
        }
    }

    /// <summary>
    /// Throws away every buffered byte. A server calls it after reading a <c>STARTTLS</c> or
    /// <c>STLS</c> line and before <see cref="IConnection.UpgradeToTlsAsync"/>, so pipelined
    /// plaintext is never run as a command (ADR-0010, section 1).
    /// </summary>
    /// <returns>How many bytes were thrown away, for the server's log note.</returns>
    public int DiscardBuffered()
    {
        var discarded = BufferedCount;
        bufferedStart = 0;
        bufferedEnd = 0;
        scannedCount = 0;

        return discarded;
    }

    /// <summary>
    /// Stops the head timeout's clock.
    /// </summary>
    public void Dispose() => StopHeadTimeout();

    // The length of the buffered line with its CRLF, or 0 when no CRLF is buffered yet. The scan
    // resumes one byte back, so a CR that ended the last scan is paired with an LF read since.
    private int FindLineLength()
    {
        var from = Math.Max(0, scannedCount - 1);
        var found = buffer.AsSpan(bufferedStart + from, BufferedCount - from).IndexOf("\r\n"u8);
        scannedCount = BufferedCount;

        return found < 0 ? 0 : from + found + 2;
    }

    private byte[] TakeLine(int lineLength)
    {
        var line = buffer.AsSpan(bufferedStart, lineLength - 2).ToArray();
        Consume(lineLength);
        StopHeadTimeout();

        return line;
    }

    private void Consume(int byteCount)
    {
        bufferedStart += byteCount;
        scannedCount = 0;
    }

    // Reads more of a line; returns why no more will come, or null when some arrived.
    private async ValueTask<CrlfLineReadOutcome?> FillLineAsync(CancellationToken cancellationToken)
    {
        var room = maxLineBytes > 0 ? maxLineBytes - BufferedCount : int.MaxValue;
        int read;
        try
        {
            read = await ReadMoreAsync((int)Math.Min(room, int.MaxValue), underHeadTimeout: true, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CrlfLineReadOutcome.HeadTimedOut;
        }

        if (read == 0)
        {
            return CrlfLineReadOutcome.Closed;
        }

        StartHeadTimeout();

        return null;
    }

    // Reads at most maxBytes more into the buffer; a line's read is under the head timeout while its clock runs.
    private async ValueTask<int> ReadMoreAsync(int maxBytes, bool underHeadTimeout, CancellationToken cancellationToken)
    {
        MakeRoomToRead();
        var destination = buffer.AsMemory(bufferedEnd, Math.Min(maxBytes, buffer.Length - bufferedEnd));
        var read = await ReadWithinAsync(destination, underHeadTimeout ? headTimeoutClock : null, cancellationToken);
        bufferedEnd += read;

        return read;
    }

    private async ValueTask<int> ReadWithinAsync(Memory<byte> destination, CancellationTokenSource? clock, CancellationToken cancellationToken)
    {
        if (clock is null)
        {
            return await connection.ReadAsync(destination, cancellationToken);
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, clock.Token);

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

    private void StartHeadTimeout()
    {
        if (headTimeoutClock is null && headTimeout != Timeout.InfiniteTimeSpan)
        {
            headTimeoutClock = new CancellationTokenSource(headTimeout, timeProvider);
        }
    }

    private void StopHeadTimeout()
    {
        headTimeoutClock?.Dispose();
        headTimeoutClock = null;
    }
}
