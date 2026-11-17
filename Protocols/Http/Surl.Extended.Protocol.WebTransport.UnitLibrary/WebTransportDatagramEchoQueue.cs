using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// The HTTP datagrams of one WebTransport session that wait to be echoed (ADR-0117 decision 8): at
/// most <see cref="Capacity"/> of them, the newest dropped once the queue is full.
/// </summary>
internal sealed class WebTransportDatagramEchoQueue
{
    /// <summary>The most datagrams one session holds while they wait for their echo.</summary>
    public const int Capacity = 64;

    private readonly Channel<ReadOnlyMemory<byte>> waiting = Channel.CreateBounded<ReadOnlyMemory<byte>>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = true });

    /// <summary>Queues a datagram for its echo.</summary>
    /// <param name="payload">The datagram, quarter stream ID already removed.</param>
    /// <returns><see langword="true"/> when it waits for its echo; <see langword="false"/> when the queue was full and it is dropped.</returns>
    public bool TryEnqueue(ReadOnlyMemory<byte> payload) => waiting.Writer.TryWrite(payload);

    /// <summary>Queues a datagram for its echo, noting in the log one that is dropped.</summary>
    /// <param name="payload">The datagram, quarter stream ID already removed.</param>
    /// <param name="log">Where a drop is noted.</param>
    /// <param name="sessionId">The session the datagram belongs to.</param>
    public void Enqueue(ReadOnlyMemory<byte> payload, IExchangeLog log, ulong sessionId)
    {
        if (!TryEnqueue(payload))
        {
            log.Note($"A datagram of {payload.Length} bytes of session {sessionId} was dropped: {Capacity} datagrams already wait for their echo.");
        }
    }

    /// <summary>Ends the queue: what waits is still echoed, nothing more is taken.</summary>
    public void Complete() => waiting.Writer.TryComplete();

    /// <summary>Takes the oldest waiting datagram, waiting for one to arrive.</summary>
    /// <param name="cancellationToken">Cancelled when the session ends.</param>
    /// <returns>The datagram, or <see langword="null"/> once the queue is completed and empty or <paramref name="cancellationToken"/> is cancelled.</returns>
    public async ValueTask<ReadOnlyMemory<byte>?> TakeNextAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await waiting.Reader.ReadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }
}
