using System.Net;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// A connection the test talks through while the server runs: the test sends bytes whenever it
/// likes and reads what the server wrote as it arrives, so a client can answer what the server
/// said - as a client after <c>NEWKEYS</c> must, its keys depending on the server's key
/// exchange reply. It opens nothing.
/// </summary>
internal sealed class SshTestPipeConnection : IConnection
{
    private readonly Channel<byte[]> inbound = Channel.CreateUnbounded<byte[]>();
    private readonly Channel<byte[]> outbound = Channel.CreateUnbounded<byte[]>();
    private byte[] pendingInbound = [];
    private int pendingInboundOffset;
    private byte[] pendingOutbound = [];
    private int pendingOutboundOffset;

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 47301);

    public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

    public TlsSession? TlsSession => null;

    public bool WritesCompleted { get; private set; }

    /// <summary>What the client sends next.</summary>
    public void Send(byte[] bytes) => inbound.Writer.TryWrite(bytes);

    /// <summary>The client half-closes: the server's reads return 0 once they have had everything sent.</summary>
    public void CloseClientWrites() => inbound.Writer.TryComplete();

    /// <summary>
    /// Waits for exactly <paramref name="count"/> bytes the server wrote.
    /// </summary>
    /// <returns>The bytes, or <see langword="null"/> if the server finished writing first.</returns>
    public async Task<byte[]?> ReadServerBytesAsync(int count, CancellationToken cancellationToken)
    {
        var bytes = new byte[count];
        var filled = 0;
        while (filled < count)
        {
            if (pendingOutboundOffset == pendingOutbound.Length)
            {
                if (!await outbound.Reader.WaitToReadAsync(cancellationToken))
                {
                    return null;
                }

                outbound.Reader.TryRead(out pendingOutbound!);
                pendingOutboundOffset = 0;
                continue;
            }

            var taken = Math.Min(count - filled, pendingOutbound.Length - pendingOutboundOffset);
            pendingOutbound.AsSpan(pendingOutboundOffset, taken).CopyTo(bytes.AsSpan(filled));
            pendingOutboundOffset += taken;
            filled += taken;
        }

        return bytes;
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
        outbound.Writer.TryWrite(bytes.ToArray());

        return ValueTask.CompletedTask;
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken)
    {
        WritesCompleted = true;
        outbound.Writer.TryComplete();

        return ValueTask.CompletedTask;
    }

    public void Abort()
    {
        inbound.Writer.TryComplete();
        outbound.Writer.TryComplete();
    }

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync()
    {
        Abort();

        return ValueTask.CompletedTask;
    }
}
