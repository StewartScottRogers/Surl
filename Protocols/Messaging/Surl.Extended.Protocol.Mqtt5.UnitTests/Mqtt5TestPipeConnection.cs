using System.Net;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// A connection the test talks through while the broker runs: the client sends packets whenever
/// the test likes and reads the broker's packets as they arrive, so a subscriber can answer a
/// <c>PUBLISH</c> the broker sent it. It opens nothing.
/// </summary>
internal sealed class Mqtt5TestPipeConnection : IConnection
{
    private readonly Channel<byte[]> inbound = Channel.CreateUnbounded<byte[]>();
    private readonly Channel<byte> outbound = Channel.CreateUnbounded<byte>();
    private byte[] pendingInbound = [];
    private int pendingInboundOffset;

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 1883);

    public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

    public TlsSession? TlsSession => null;

    /// <summary>What the client sends next.</summary>
    public void Send(byte[] bytes) => inbound.Writer.TryWrite(bytes);

    /// <summary>
    /// Waits for the broker's next packet: its first byte, its Remaining Length and its body.
    /// </summary>
    /// <returns>The packet's bytes.</returns>
    public async Task<byte[]> ReadPacketAsync(CancellationToken cancellationToken)
    {
        var packet = new List<byte> { await outbound.Reader.ReadAsync(cancellationToken) };
        var remaining = 0;
        var shift = 0;
        byte lengthByte;
        do
        {
            lengthByte = await outbound.Reader.ReadAsync(cancellationToken);
            packet.Add(lengthByte);
            remaining |= (lengthByte & 0x7F) << shift;
            shift += 7;
        }
        while ((lengthByte & 0x80) != 0);

        for (var index = 0; index < remaining; index++)
        {
            packet.Add(await outbound.Reader.ReadAsync(cancellationToken));
        }

        return [.. packet];
    }

    /// <summary>Every byte the broker wrote that no <see cref="ReadPacketAsync"/> has taken.</summary>
    public byte[] UnreadServerBytes()
    {
        var bytes = new List<byte>();
        while (outbound.Reader.TryRead(out var value))
        {
            bytes.Add(value);
        }

        return [.. bytes];
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (pendingInboundOffset == pendingInbound.Length)
        {
            if (!await inbound.Reader.WaitToReadAsync(cancellationToken))
            {
                return 0;
            }

            inbound.Reader.TryRead(out pendingInbound!);
            pendingInboundOffset = 0;
        }

        var taken = Math.Min(buffer.Length, pendingInbound.Length - pendingInboundOffset);
        pendingInbound.AsSpan(pendingInboundOffset, taken).CopyTo(buffer.Span);
        pendingInboundOffset += taken;
        return taken;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var value in bytes.Span)
        {
            outbound.Writer.TryWrite(value);
        }

        return ValueTask.CompletedTask;
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
