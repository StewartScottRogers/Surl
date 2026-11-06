using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// Everything the server writes to one connection, in order, through one writer (ADR-0118
/// decision 8, "a slow consumer"): a bus delivery is queued here without blocking, and the bytes
/// queued and not yet taken by the transport are counted. Past the bound the outbox overflows: it
/// takes nothing more, and the write in progress is cancelled.
/// </summary>
/// <param name="maxPendingBytes">The most bytes queued and not yet written.</param>
internal sealed class StompOutbox(long maxPendingBytes) : IDisposable
{
    private readonly Channel<byte[]> queued = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource overflow = new();
    private long pendingBytes;
    private int overflowed;
    private long enqueuedCount;

    /// <summary>Whether more than the bound waited unwritten, so the connection is a slow consumer.</summary>
    public bool HasOverflowed => Volatile.Read(ref overflowed) == 1;

    /// <summary>How many writes have been queued so far, so a heart-beat can tell whether anything else was.</summary>
    public long EnqueuedCount => Interlocked.Read(ref enqueuedCount);

    /// <summary>
    /// Queues bytes to be written after everything queued before them; never blocks. Past the
    /// bound, or once the outbox is complete, they are dropped, and the first time past the bound
    /// the outbox overflows.
    /// </summary>
    public void Enqueue(byte[] bytes)
    {
        var pending = Interlocked.Add(ref pendingBytes, bytes.Length);
        if (pending <= maxPendingBytes && queued.Writer.TryWrite(bytes))
        {
            Interlocked.Increment(ref enqueuedCount);
            return;
        }

        // Dropped: past the bound it overflows; after Complete it is only too late.
        Interlocked.Add(ref pendingBytes, -bytes.Length);
        if (pending > maxPendingBytes && Interlocked.Exchange(ref overflowed, 1) == 0)
        {
            queued.Writer.TryComplete();
            // Cancelled off this thread: a bus delivery calls Enqueue and must not run the writer.
            _ = overflow.CancelAsync();
        }
    }

    /// <summary>Takes nothing more; the writer ends once it has written what is queued.</summary>
    public void Complete() => queued.Writer.TryComplete();

    /// <summary>Releases the overflow's cancellation, once the writer has ended.</summary>
    public void Dispose() => overflow.Dispose();

    /// <summary>
    /// Writes everything queued, in order, until the outbox is complete and empty or it overflows.
    /// </summary>
    /// <param name="connection">The connection written to.</param>
    /// <param name="cancellationToken">Ends the writing with an <see cref="OperationCanceledException"/>.</param>
    /// <returns>A task that completes when the writing ends, or fails as the transport's write did.</returns>
    public async Task WriteAllAsync(IConnection connection, CancellationToken cancellationToken)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, overflow.Token);
        try
        {
            await foreach (var bytes in queued.Reader.ReadAllAsync(cancellation.Token))
            {
                await connection.WriteAsync(bytes, cancellation.Token);
                Interlocked.Add(ref pendingBytes, -bytes.Length);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Only the overflow cancels the linked token without the caller's.
        }
    }
}
