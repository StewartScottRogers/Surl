using System.Net;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// A connection a test talks through while the server runs: the client sends packets when the
/// test likes and reads the server's packets as they arrive. It opens nothing.
/// </summary>
internal sealed class MqttTestPipeConnection : IConnection
{
    private readonly Channel<byte[]> inbound = Channel.CreateUnbounded<byte[]>();
    private readonly Channel<byte[]> outbound = Channel.CreateUnbounded<byte[]>();
    private readonly TaskCompletionSource writeBlocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private byte[] pendingInbound = [];
    private int pendingInboundOffset;
    private int readsWaiting;

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 1883);

    public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

    public TlsSession? TlsSession => null;

    /// <summary>Run with each packet before it counts as written, on the server's thread.</summary>
    public Action<byte[]> Writing { get; set; } = _ => { };

    /// <summary>Whether the client never takes a packet, so its write ends only when cancelled.</summary>
    public Func<byte[], bool> BlocksWriteOf { get; set; } = _ => false;

    /// <summary>Completes once a write <see cref="BlocksWriteOf"/> chose is waiting.</summary>
    public Task WriteBlocked => writeBlocked.Task;

    /// <summary>How many of the server's reads are waiting for the client.</summary>
    public int ReadsWaiting => Volatile.Read(ref readsWaiting);

    /// <summary>What the client sends next.</summary>
    public void Send(byte[] bytes) => inbound.Writer.TryWrite(bytes);

    /// <summary>The client half-closes.</summary>
    public void CloseFromClient() => inbound.Writer.TryComplete();

    /// <summary>Waits for the server's next write, each of which is one packet.</summary>
    public async Task<byte[]> ReadPacketAsync(CancellationToken cancellationToken) =>
        await outbound.Reader.ReadAsync(cancellationToken);

    /// <summary>Every packet the server wrote that no <see cref="ReadPacketAsync"/> has taken.</summary>
    public IReadOnlyList<byte[]> UnreadServerPackets()
    {
        var packets = new List<byte[]>();
        while (outbound.Reader.TryRead(out var packet))
        {
            packets.Add(packet);
        }

        return packets;
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (pendingInboundOffset == pendingInbound.Length)
        {
            Interlocked.Increment(ref readsWaiting);
            try
            {
                if (!await inbound.Reader.WaitToReadAsync(cancellationToken))
                {
                    return 0;
                }
            }
            finally
            {
                Interlocked.Decrement(ref readsWaiting);
            }

            inbound.Reader.TryRead(out pendingInbound!);
            pendingInboundOffset = 0;
        }

        var taken = Math.Min(buffer.Length, pendingInbound.Length - pendingInboundOffset);
        pendingInbound.AsSpan(pendingInboundOffset, taken).CopyTo(buffer.Span);
        pendingInboundOffset += taken;
        return taken;
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var packet = bytes.ToArray();
        if (BlocksWriteOf(packet))
        {
            writeBlocked.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        Writing(packet);
        outbound.Writer.TryWrite(packet);
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public void Abort() => inbound.Writer.TryComplete();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync()
    {
        Abort();
        return ValueTask.CompletedTask;
    }
}
