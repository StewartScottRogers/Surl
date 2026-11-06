namespace Surl.Http2;

/// <summary>
/// One open request stream of an HTTP/2 connection: its body, what is left of the receive
/// window the server granted it, and the cancellation that ends its answer when the client
/// resets it or the connection ends (RFC 9113 section 5.1).
/// </summary>
internal sealed class Http2Stream : IDisposable
{
    private readonly CancellationTokenSource reset;
    private readonly Lock gate = new();
    private bool isDisposed;
    private long receiveWindow = Http2SendWindows.DefaultInitialWindowSize;

    /// <summary>
    /// Opens stream <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The stream identifier, odd.</param>
    /// <param name="body">The request's body.</param>
    /// <param name="connectionToken">The connection's cancellation, which ends the stream too.</param>
    public Http2Stream(int id, Http2RequestBody body, CancellationToken connectionToken)
    {
        Id = id;
        Body = body;
        reset = CancellationTokenSource.CreateLinkedTokenSource(connectionToken);
    }

    /// <summary>The stream identifier.</summary>
    public int Id { get; }

    /// <summary>The request's body.</summary>
    public Http2RequestBody Body { get; }

    /// <summary>Cancelled when the client resets the stream or the connection ends.</summary>
    public CancellationToken Token => reset.Token;

    /// <summary>
    /// Takes a <c>DATA</c> frame's whole payload, padding included, from the receive window.
    /// </summary>
    /// <param name="payloadLength">The payload length.</param>
    /// <returns><see langword="false"/> when the frame is past the window the server granted.</returns>
    public bool TakeFromReceiveWindow(int payloadLength) =>
        Interlocked.Add(ref receiveWindow, -payloadLength) >= 0;

    /// <summary>
    /// Gives back to the receive window the bytes the server replenished with <c>WINDOW_UPDATE</c>.
    /// </summary>
    /// <param name="increment">The increment sent.</param>
    public void GiveBackToReceiveWindow(int increment) => Interlocked.Add(ref receiveWindow, increment);

    /// <summary>
    /// Ends the stream: its answer is cut off and its body ends early.
    /// </summary>
    public void Reset()
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                reset.Cancel();
            }
        }

        Body.Abandon();
    }

    /// <summary>
    /// Releases the stream's cancellation once its answer is over; a later
    /// <see cref="Reset"/> only ends the body.
    /// </summary>
    public void Dispose()
    {
        lock (gate)
        {
            isDisposed = true;
            reset.Dispose();
        }
    }
}
