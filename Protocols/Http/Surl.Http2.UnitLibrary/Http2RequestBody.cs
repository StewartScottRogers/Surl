using System.Threading.Channels;

namespace Surl.Http2;

/// <summary>
/// The body of a request on one HTTP/2 stream: the stream's <c>DATA</c>, which the connection's
/// reader hands over as it arrives and the stream's handler reads piece by piece (RFC 9113
/// section 8.1; ADR-0081, decision 7; ADR-0086, decision 1).
/// </summary>
/// <remarks>
/// A body is announced when the request's <c>HEADERS</c> did not end the stream. Each piece the
/// handler has read is handed back to the replenish callback when it asks for the next one, which
/// reopens the stream's receive window by that much, so a client never sends more than the
/// handler has taken. A <c>content-length</c> the <c>DATA</c> does not match sets
/// <see cref="HasContentLengthMismatch"/> once the body has been read to its end.
/// </remarks>
internal sealed class Http2RequestBody
{
    private readonly Channel<byte[]> pieces = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly Func<int, Task> replenishAsync;
    private readonly Lock endingGate = new();
    private volatile bool hasEnded;
    private long received;
    private int unreplenished;

    /// <summary>
    /// Creates the body of a request whose <c>HEADERS</c> announced one or ended the stream.
    /// </summary>
    /// <param name="isAnnounced">Whether the <c>HEADERS</c> left the stream open for <c>DATA</c>.</param>
    /// <param name="contentLength">The request's <c>content-length</c>, or <see langword="null"/> when it sent none.</param>
    /// <param name="replenishAsync">Reopens the stream's receive window by the bytes the handler has read.</param>
    public Http2RequestBody(bool isAnnounced, long? contentLength, Func<int, Task> replenishAsync)
    {
        IsAnnounced = isAnnounced;
        AnnouncedLength = contentLength;
        this.replenishAsync = replenishAsync;
        if (!isAnnounced)
        {
            End();
        }
    }

    /// <summary>Whether the request's <c>HEADERS</c> left the stream open for a body.</summary>
    public bool IsAnnounced { get; }

    /// <summary>The request's <c>content-length</c>, or <see langword="null"/> when it sent none.</summary>
    public long? AnnouncedLength { get; }

    /// <summary>
    /// Whether the client may still send <c>DATA</c> for the body: it has not ended the stream,
    /// reset it, or closed the connection.
    /// </summary>
    public bool IsStillArriving => !hasEnded;

    /// <summary>Whether the client reset the stream or closed the connection before ending the body.</summary>
    public bool HasEndedEarly { get; private set; }

    /// <summary>Whether the <c>DATA</c> read to the end did not add up to the request's <c>content-length</c>.</summary>
    public bool HasContentLengthMismatch { get; private set; }

    /// <summary>
    /// Hands over one <c>DATA</c> frame's content, padding removed.
    /// </summary>
    /// <param name="data">The bytes.</param>
    public void Receive(byte[] data) => pieces.Writer.TryWrite(data);

    /// <summary>The client's <c>END_STREAM</c>: the body is whole.</summary>
    public void End() => Finish(endedEarly: false);

    /// <summary>The client reset the stream or closed the connection: the body ends early.</summary>
    public void Abandon() => Finish(endedEarly: true);

    /// <summary>
    /// Ends the body once: the first ending wins, and <see cref="HasEndedEarly"/> is set before the
    /// pieces complete, so a reader the completion wakes on another thread always sees it.
    /// </summary>
    /// <param name="endedEarly">Whether the client reset the stream or closed the connection.</param>
    private void Finish(bool endedEarly)
    {
        lock (endingGate)
        {
            if (hasEnded)
            {
                return;
            }

            HasEndedEarly = endedEarly;
            hasEnded = true;
        }

        pieces.Writer.TryComplete();
    }

    /// <summary>
    /// Replenishes the window by the piece read before, then waits for the next piece.
    /// </summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>The next piece, or <see langword="null"/> once the body has ended, whole or early.</returns>
    public async Task<byte[]?> ReadAsync(CancellationToken cancellationToken)
    {
        if (unreplenished > 0)
        {
            var increment = unreplenished;
            unreplenished = 0;
            await replenishAsync(increment);
        }

        if (!await pieces.Reader.WaitToReadAsync(cancellationToken))
        {
            HasContentLengthMismatch = !HasEndedEarly && AnnouncedLength is { } length && length != received;

            return null;
        }

        var piece = await pieces.Reader.ReadAsync(cancellationToken);
        received += piece.Length;
        unreplenished = piece.Length;

        return piece;
    }
}
