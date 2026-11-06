using System.Net;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// curl's side of a connection a test feeds while the proxy runs: <see cref="Send"/> hands the
/// proxy the next bytes, <see cref="HalfClose"/> ends them, and every byte the proxy writes is
/// kept in <see cref="WrittenBytes"/>. Secured from the start with the given TLS session, as the
/// serving engine hands the HTTPS proxy its connections.
/// </summary>
internal sealed class InteractiveClientConnection(TlsSession tlsSession) : IConnection
{
    private readonly Channel<byte[]> inbound = Channel.CreateUnbounded<byte[]>();
    private readonly MemoryStream written = new();
    private readonly Lock gate = new();
    private byte[] pending = [];

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 3129);

    public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

    public TlsSession? TlsSession { get; } = tlsSession;

    public byte[] WrittenBytes
    {
        get
        {
            lock (gate)
            {
                return written.ToArray();
            }
        }
    }

    public void Send(params byte[][] chunks)
    {
        foreach (var chunk in chunks)
        {
            inbound.Writer.TryWrite(chunk);
        }
    }

    public void HalfClose() => inbound.Writer.TryComplete();

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (pending.Length == 0)
        {
            if (!await inbound.Reader.WaitToReadAsync(cancellationToken))
            {
                return 0;
            }

            pending = await inbound.Reader.ReadAsync(cancellationToken);
        }

        var count = Math.Min(pending.Length, buffer.Length);
        pending.AsSpan(0, count).CopyTo(buffer.Span);
        pending = pending[count..];

        return count;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            written.Write(bytes.Span);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public void Abort() => HalfClose();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("The connection is secured from the start.");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
