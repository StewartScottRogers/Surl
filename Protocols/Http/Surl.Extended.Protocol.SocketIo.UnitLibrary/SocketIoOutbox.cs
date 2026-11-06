using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// The bytes waiting to be written to one session's connection, in the order they were queued, at
/// most <paramref name="maxUnsentBytes"/> at a time (ADR-0120, decision 7). Each queued item is
/// written whole, so an event and its attachments are never split by another session's broadcast.
/// </summary>
/// <param name="maxUnsentBytes">The most bytes queued and not yet written.</param>
internal sealed class SocketIoOutbox(long maxUnsentBytes)
{
    /// <summary>
    /// The most bytes a session may have unsent (ADR-0120, decision 7).
    /// </summary>
    public const long MaxUnsentBytes = 1048576;

    private readonly Channel<byte[]> queue = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    private long unsentBytes;

    /// <summary>
    /// Queues <paramref name="bytes"/>, unless that would leave more than the limit unsent.
    /// </summary>
    /// <returns><see langword="false"/> when the limit refused it.</returns>
    public bool TryQueue(byte[] bytes)
    {
        if (Interlocked.Add(ref unsentBytes, bytes.Length) > maxUnsentBytes)
        {
            Interlocked.Add(ref unsentBytes, -bytes.Length);

            return false;
        }

        // Once completed, the session is ending and what comes after is dropped.
        queue.Writer.TryWrite(bytes);

        return true;
    }

    /// <summary>
    /// Takes no more: <see cref="WriteAllAsync"/> returns once what is queued is written.
    /// </summary>
    public void Complete() => queue.Writer.TryComplete();

    /// <summary>
    /// Writes each queued item to <paramref name="connection"/> until <see cref="Complete"/>.
    /// </summary>
    public async Task WriteAllAsync(IConnection connection, CancellationToken cancellationToken)
    {
        await foreach (var bytes in queue.Reader.ReadAllAsync(cancellationToken))
        {
            await connection.WriteAsync(bytes, cancellationToken);
            Interlocked.Add(ref unsentBytes, -bytes.Length);
        }
    }
}
