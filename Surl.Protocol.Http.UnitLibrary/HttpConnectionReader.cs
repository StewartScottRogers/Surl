using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// Reads HTTP/1.x request heads from one connection, one after another, and hands back the
/// bytes that follow each head untouched.
/// </summary>
/// <remarks>
/// Reads from the connection arrive in whatever pieces the transport delivers, so this
/// reader buffers: bytes read past the end of a head stay buffered and are returned first by
/// the next <see cref="ReadRequestHeadAsync"/> or <see cref="ReadAsync"/>. A head is at most
/// <see cref="MaximumRequestHeadBytes"/> bytes. A line ends at LF, with or without a CR
/// before it (RFC 9112, section 2.2). It is not safe for concurrent calls.
/// <para>
/// After any outcome but <see cref="HttpRequestHeadReadOutcome.HeadRead"/>, or after a read
/// throws, the reader stands part way through a head whose earlier lines are gone, so the
/// caller must stop reading and close the connection.
/// </para>
/// </remarks>
public sealed class HttpConnectionReader
{
    /// <summary>
    /// The most bytes one request head may take, from its first byte (empty lines before the
    /// request line included) to the LF of the empty line that ends it: 307,200, the
    /// 300 KiB upstream curl itself allows a response head. A longer head is
    /// <see cref="HttpRequestHeadReadOutcome.HeadTooLarge"/>.
    /// </summary>
    public const int MaximumRequestHeadBytes = 300 * 1024;

    private const int InitialBufferBytes = 4096;

    private readonly IConnection connection;
    private byte[] buffer = new byte[InitialBufferBytes];
    private int bufferedStart;
    private int bufferedEnd;

    /// <summary>
    /// Creates a reader over <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">The connection to read from.</param>
    public HttpConnectionReader(IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        this.connection = connection;
    }

    private int BufferedCount => bufferedEnd - bufferedStart;

    /// <summary>
    /// Reads the next request head, leaving every byte after it unread.
    /// </summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The head, or the named reason there is none.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async ValueTask<HttpRequestHeadReadResult> ReadRequestHeadAsync(CancellationToken cancellationToken)
    {
        var lines = new HttpRequestHeadLineReader();
        var headBytes = 0;
        var scannedCount = 0;
        HttpRequestHeadReadResult? result = null;

        while (result is null)
        {
            var lineFeed = Array.IndexOf(buffer, (byte)'\n', bufferedStart + scannedCount, BufferedCount - scannedCount);
            if (lineFeed < 0)
            {
                scannedCount = BufferedCount;
                result = await FillOrStopAsync(headBytes, lines.HasRequestLine, cancellationToken);
                continue;
            }

            var lineLength = lineFeed + 1 - bufferedStart;
            scannedCount = 0;
            headBytes += lineLength;
            result = headBytes > MaximumRequestHeadBytes
                ? HttpRequestHeadReadResult.NoHead(HttpRequestHeadReadOutcome.HeadTooLarge)
                : lines.AcceptLine(buffer.AsSpan(bufferedStart, lineLength - 1));
            bufferedStart += lineLength;
        }

        return result;
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

    private async ValueTask<HttpRequestHeadReadResult?> FillOrStopAsync(int headBytes, bool hasRequestLine, CancellationToken cancellationToken)
    {
        var pendingHeadBytes = headBytes + BufferedCount;
        var onlyEmptyLinesSoFar = !hasRequestLine && BufferedCount == 0;
        if (pendingHeadBytes >= MaximumRequestHeadBytes)
        {
            return HttpRequestHeadReadResult.NoHead(HttpRequestHeadReadOutcome.HeadTooLarge);
        }

        MakeRoomToRead();
        var read = await connection.ReadAsync(buffer.AsMemory(bufferedEnd), cancellationToken);
        bufferedEnd += read;
        if (read > 0)
        {
            return null;
        }

        return HttpRequestHeadReadResult.NoHead(onlyEmptyLinesSoFar
            ? HttpRequestHeadReadOutcome.ConnectionClosed
            : HttpRequestHeadReadOutcome.ConnectionClosedBeforeHeadEnded);
    }

    private void MakeRoomToRead()
    {
        buffer.AsSpan(bufferedStart, BufferedCount).CopyTo(buffer);
        bufferedEnd = BufferedCount;
        bufferedStart = 0;

        if (bufferedEnd == buffer.Length)
        {
            Array.Resize(ref buffer, Math.Min(buffer.Length * 2, MaximumRequestHeadBytes));
        }
    }
}
