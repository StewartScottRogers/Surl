using Surl.Protocol.Abstractions;

namespace Surl.HttpMessage;

/// <summary>
/// Reads HTTP/1.x request heads, or the request heads of a protocol that borrows their syntax
/// (<see cref="HttpMessageProtocol"/>), from one connection, one after another, and hands back the
/// bytes that follow each head untouched.
/// </summary>
/// <remarks>
/// Reads from the connection arrive in whatever pieces the transport delivers, so this
/// reader buffers: bytes read past the end of a head stay buffered and are returned first by
/// the next <see cref="ReadRequestHeadAsync"/>, <see cref="ReadLineAsync"/> or
/// <see cref="ReadAsync"/>. A head is at most the request-head limit the reader was created
/// with, and the reader never takes more than that limit from the connection while it reads
/// one (ADR-0006, section 5). A line ends at LF, with or without a CR before it (RFC 9112,
/// section 2.2). It is not safe for concurrent calls.
/// <para>
/// After any outcome but <see cref="HttpRequestHeadReadOutcome.HeadRead"/>, or after a read
/// throws, the reader stands part way through a head whose earlier lines are gone, so the
/// caller must stop reading and close the connection.
/// </para>
/// </remarks>
public sealed class HttpConnectionReader
{
    private const int InitialBufferBytes = 4096;

    private static readonly TimeSpan MaxTimerDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1.0);

    private readonly IConnection connection;
    private readonly HttpMessageProtocol protocol;
    private readonly int requestHeadLimit;
    private byte[] buffer = new byte[InitialBufferBytes];
    private int bufferedStart;
    private int bufferedEnd;
    private int currentHeadBytes;

    /// <summary>
    /// Creates a reader over <paramref name="connection"/> that reads HTTP request heads
    /// (<see cref="HttpMessageProtocol.Http11"/>).
    /// </summary>
    /// <param name="connection">The connection to read from.</param>
    /// <param name="maxRequestHeadBytes">
    /// The most bytes one request head may take, from its first byte (empty lines before the
    /// request line included) to the LF of the empty line that ends it
    /// (<see cref="ExchangeLimits.MaxRequestHeadBytes"/>). 0 means no limit, which in practice
    /// is <see cref="Array.MaxLength"/>, the most one buffer can hold; so is any larger value.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxRequestHeadBytes"/> is negative.</exception>
    public HttpConnectionReader(IConnection connection, long maxRequestHeadBytes)
        : this(connection, maxRequestHeadBytes, HttpMessageProtocol.Http11)
    {
    }

    /// <summary>
    /// Creates a reader over <paramref name="connection"/> that reads request heads whose
    /// request line names <paramref name="protocol"/>.
    /// </summary>
    /// <param name="connection">The connection to read from.</param>
    /// <param name="maxRequestHeadBytes">
    /// The most bytes one request head may take, as for
    /// <see cref="HttpConnectionReader(IConnection, long)"/>.
    /// </param>
    /// <param name="protocol">The protocol a request line's version must name.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxRequestHeadBytes"/> is negative.</exception>
    public HttpConnectionReader(IConnection connection, long maxRequestHeadBytes, HttpMessageProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRequestHeadBytes);
        ArgumentNullException.ThrowIfNull(protocol);

        this.connection = connection;
        this.protocol = protocol;
        requestHeadLimit = maxRequestHeadBytes == 0 ? Array.MaxLength : (int)Math.Min(maxRequestHeadBytes, Array.MaxLength);
    }

    /// <summary>
    /// Whether any byte of the head being read, or of the next one, has arrived: read into
    /// the head so far, or buffered and not yet read. After a head read was cut off, it says
    /// whether the client had started sending that head.
    /// </summary>
    public bool HasReceivedHeadBytes => currentHeadBytes + BufferedCount > 0;

    private int BufferedCount => bufferedEnd - bufferedStart;

    /// <summary>
    /// Waits until at least one byte is buffered, reading from the connection only when none
    /// is, so the caller can tell a client that starts another request from one that closes.
    /// </summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns><see langword="true"/> once a byte is buffered; <see langword="false"/> when the client half-closed first.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async ValueTask<bool> WaitForBytesAsync(CancellationToken cancellationToken) =>
        BufferedCount > 0 || await FillAsync(requestHeadLimit, cancellationToken) > 0;

    /// <summary>
    /// Waits as <see cref="WaitForBytesAsync"/> does, then returns the next byte without taking
    /// it, so the caller can tell what follows - an RTSP interleaved frame's <c>$</c> from a
    /// request head (RFC 2326 section 10.12) - before choosing how to read it.
    /// </summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>The next byte, still buffered; -1 when the client half-closed first.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async ValueTask<int> PeekByteAsync(CancellationToken cancellationToken) =>
        await WaitForBytesAsync(cancellationToken) ? buffer[bufferedStart] : -1;

    /// <summary>
    /// Reads the next request head of an exchange within its head timeout
    /// (<see cref="ExchangeLimits.HeadTimeout"/>, on <see cref="ExchangeContext.TimeProvider"/>;
    /// ADR-0006 section 1, ADR-0070 decision 3).
    /// </summary>
    /// <remarks>
    /// The first head is timed from this call. A later one is timed from its first byte, and
    /// the wait for that byte is not timed here: a client that half-closes before sending it
    /// ends the connection with <see cref="HttpRequestHeadReadOutcome.ConnectionClosed"/>. A
    /// timeout past <see cref="uint.MaxValue"/> - 1 milliseconds, about 49.7 days, is the most a
    /// timer can wait and is treated as none.
    /// </remarks>
    /// <param name="context">The exchange: its limits, clock and cancellation.</param>
    /// <param name="isFirstHead">Whether this is the connection's first head.</param>
    /// <returns>
    /// The head, or the named reason there is none: <see cref="HttpRequestHeadReadOutcome.HeadTimedOut"/>
    /// when the timeout ran out after a byte of the head arrived,
    /// <see cref="HttpRequestHeadReadOutcome.HeadTimedOutBeforeAnyByte"/> when none had.
    /// </returns>
    /// <exception cref="OperationCanceledException">The exchange's own cancellation token cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async Task<HttpRequestHeadReadResult> ReadNextRequestHeadAsync(ExchangeContext context, bool isFirstHead)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!isFirstHead && !await WaitForBytesAsync(context.CancellationToken))
        {
            return HttpRequestHeadReadResult.NoHead(HttpRequestHeadReadOutcome.ConnectionClosed);
        }

        using var headTimeout = new CancellationTokenSource(TimerDelay(context.Limits.HeadTimeout), context.TimeProvider);
        using var headTimeoutOrExchange = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headTimeout.Token);
        try
        {
            return await ReadRequestHeadAsync(headTimeoutOrExchange.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            return HttpRequestHeadReadResult.NoHead(HasReceivedHeadBytes
                ? HttpRequestHeadReadOutcome.HeadTimedOut
                : HttpRequestHeadReadOutcome.HeadTimedOutBeforeAnyByte);
        }
    }

    /// <summary>
    /// Reads the next request head, leaving every byte after it unread.
    /// </summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The head, or the named reason there is none.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async ValueTask<HttpRequestHeadReadResult> ReadRequestHeadAsync(CancellationToken cancellationToken)
    {
        var lines = new HttpRequestHeadLineReader(protocol);
        var scannedCount = 0;
        HttpRequestHeadReadResult? result = null;
        currentHeadBytes = 0;

        while (result is null)
        {
            var lineFeed = Array.IndexOf(buffer, (byte)'\n', bufferedStart + scannedCount, BufferedCount - scannedCount);
            if (lineFeed < 0)
            {
                scannedCount = BufferedCount;
                result = await FillOrStopAsync(lines.HasRequestLine, cancellationToken);
                continue;
            }

            var lineLength = lineFeed + 1 - bufferedStart;
            scannedCount = 0;
            currentHeadBytes += lineLength;
            result = currentHeadBytes > requestHeadLimit
                ? HttpRequestHeadReadResult.NoHead(HttpRequestHeadReadOutcome.HeadTooLarge)
                : lines.AcceptLine(buffer.AsSpan(bufferedStart, lineLength - 1));
            bufferedStart += lineLength;
        }

        return result;
    }

    /// <summary>
    /// Reads one line that follows the last head, such as a chunk-size line of a chunked
    /// body (RFC 9112, section 7.1).
    /// </summary>
    /// <param name="maxLineBytes">The most bytes the line may take, its LF included.</param>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>
    /// The line without its LF, a CR before the LF kept, so the caller can require CRLF;
    /// <see langword="null"/> when the line is longer than <paramref name="maxLineBytes"/>
    /// or the client half-closed before its LF. While it waits for the LF of a line that is
    /// too long, the reader reads nothing more once it holds <paramref name="maxLineBytes"/>
    /// bytes, though it may already hold more from an earlier read.
    /// </returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async ValueTask<byte[]?> ReadLineAsync(int maxLineBytes, CancellationToken cancellationToken)
    {
        var scannedCount = 0;
        var lineFeed = -1;

        while (lineFeed < 0)
        {
            lineFeed = Array.IndexOf(buffer, (byte)'\n', bufferedStart + scannedCount, BufferedCount - scannedCount);
            scannedCount = BufferedCount;
            if (lineFeed < 0 && (BufferedCount >= maxLineBytes || await FillAsync(maxLineBytes - BufferedCount, cancellationToken) == 0))
            {
                return null;
            }
        }

        var lineLength = lineFeed + 1 - bufferedStart;
        var content = buffer.AsSpan(bufferedStart, lineLength - 1);
        bufferedStart += lineLength;

        return lineLength > maxLineBytes ? null : content.ToArray();
    }

    /// <summary>
    /// Reads bytes that follow the last head: the buffered ones first, then from the
    /// connection.
    /// </summary>
    /// <param name="destination">Where the bytes go; must not be empty.</param>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>How many bytes were copied; 0 once the client has half-closed and every byte has been read.</returns>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is empty.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        if (BufferedCount == 0 || destination.IsEmpty)
        {
            return connection.ReadAsync(destination, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var count = Math.Min(BufferedCount, destination.Length);
        buffer.AsSpan(bufferedStart, count).CopyTo(destination.Span);
        bufferedStart += count;

        return ValueTask.FromResult(count);
    }

    // A timer cannot wait longer than uint.MaxValue - 1 milliseconds (about 49.7 days); a
    // head timeout past that never fires in practice, so it is treated as none.
    private static TimeSpan TimerDelay(TimeSpan headTimeout) =>
        headTimeout > MaxTimerDelay ? Timeout.InfiniteTimeSpan : headTimeout;

    private async ValueTask<HttpRequestHeadReadResult?> FillOrStopAsync(bool hasRequestLine, CancellationToken cancellationToken)
    {
        var pendingHeadBytes = currentHeadBytes + BufferedCount;
        var onlyEmptyLinesSoFar = !hasRequestLine && BufferedCount == 0;
        if (pendingHeadBytes >= requestHeadLimit)
        {
            return HttpRequestHeadReadResult.NoHead(HttpRequestHeadReadOutcome.HeadTooLarge);
        }

        if (await FillAsync(requestHeadLimit - pendingHeadBytes, cancellationToken) > 0)
        {
            return null;
        }

        return HttpRequestHeadReadResult.NoHead(onlyEmptyLinesSoFar
            ? HttpRequestHeadReadOutcome.ConnectionClosed
            : HttpRequestHeadReadOutcome.ConnectionClosedBeforeHeadEnded);
    }

    // Reads once from the connection into the buffer, at most mostBytes (at least 1).
    private async ValueTask<int> FillAsync(int mostBytes, CancellationToken cancellationToken)
    {
        MakeRoomToRead();

        var count = Math.Min(buffer.Length - bufferedEnd, mostBytes);
        var read = await connection.ReadAsync(buffer.AsMemory(bufferedEnd, count), cancellationToken);
        bufferedEnd += read;

        return read;
    }

    private void MakeRoomToRead()
    {
        buffer.AsSpan(bufferedStart, BufferedCount).CopyTo(buffer);
        bufferedEnd = BufferedCount;
        bufferedStart = 0;

        if (bufferedEnd == buffer.Length)
        {
            Array.Resize(ref buffer, (int)Math.Min(buffer.Length * 2L, Array.MaxLength));
        }
    }
}
