using System.Threading.Channels;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// What waits to be sent to one session, in the order it was queued, at most
/// <paramref name="maxUnsentBytes"/> at a time (ADR-0120, decision 7). Each item is sent whole, so
/// an event and its attachments are never split by another session's broadcast. A WebSocket writes
/// the items as frames (<see cref="WriteAllAsync"/>); HTTP long-polling takes every queued packet into
/// one response (<see cref="WaitToTakeAsync"/>, <see cref="TakeAll"/>).
/// </summary>
/// <param name="maxUnsentBytes">The most bytes queued and not yet sent.</param>
internal sealed class SocketIoOutbox(long maxUnsentBytes)
{
    /// <summary>
    /// The most bytes a session may have unsent (ADR-0120, decision 7).
    /// </summary>
    public const long MaxUnsentBytes = 1048576;

    // Several readers: a held poll left waiting when a noop answered it must not stop the next one.
    private readonly Channel<SocketIoOutboxItem> queue = Channel.CreateUnbounded<SocketIoOutboxItem>();
    private long unsentBytes;

    /// <summary>
    /// Queues <paramref name="item"/>, unless that would leave more than the limit unsent.
    /// </summary>
    /// <returns><see langword="false"/> when the limit refused it.</returns>
    public bool TryQueue(SocketIoOutboxItem item)
    {
        var size = item.Size;
        if (Interlocked.Add(ref unsentBytes, size) > maxUnsentBytes)
        {
            Interlocked.Add(ref unsentBytes, -size);

            return false;
        }

        // Once completed, the session is ending and what comes after is dropped.
        queue.Writer.TryWrite(item);

        return true;
    }

    /// <summary>
    /// Takes no more: <see cref="WriteAllAsync"/> returns once what is queued is written, and
    /// <see cref="WaitToTakeAsync"/> answers <see langword="false"/> once it is taken.
    /// </summary>
    public void Complete() => queue.Writer.TryComplete();

    /// <summary>
    /// Writes each queued item through <paramref name="write"/> until <see cref="Complete"/>.
    /// </summary>
    public async Task WriteAllAsync(Func<SocketIoOutboxItem, CancellationToken, Task> write, CancellationToken cancellationToken)
    {
        await foreach (var item in queue.Reader.ReadAllAsync(cancellationToken))
        {
            await write(item, cancellationToken);
            Interlocked.Add(ref unsentBytes, -item.Size);
        }
    }

    /// <summary>
    /// Waits until an item is queued, or the outbox is completed and empty.
    /// </summary>
    /// <returns><see langword="false"/> when nothing more will come.</returns>
    public Task<bool> WaitToTakeAsync(CancellationToken cancellationToken) =>
        queue.Reader.WaitToReadAsync(cancellationToken).AsTask();

    /// <summary>
    /// Takes every queued item's packets, in order.
    /// </summary>
    public List<EngineIoPacket> TakeAll()
    {
        var packets = new List<EngineIoPacket>();
        while (queue.Reader.TryRead(out var item))
        {
            packets.AddRange(item.Packets);
            Interlocked.Add(ref unsentBytes, -item.Size);
        }

        return packets;
    }
}
