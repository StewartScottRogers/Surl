using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// A connection over an <see cref="InMemoryConnection"/> that serves its script up to
/// <paramref name="bytesBeforeGate"/> bytes, then holds every read until <paramref name="gate"/>
/// completes - a client that stays connected while another connection acts.
/// </summary>
internal sealed class UnitTestGatedConnection(InMemoryConnection inner, int bytesBeforeGate, Task gate) : IConnection
{
    private int bytesLeftBeforeGate = bytesBeforeGate;

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    public byte[] WrittenBytes => inner.WrittenBytes;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (bytesLeftBeforeGate == 0)
        {
            await gate.WaitAsync(cancellationToken);
            return await inner.ReadAsync(buffer, cancellationToken);
        }

        var read = await inner.ReadAsync(buffer[..Math.Min(buffer.Length, bytesLeftBeforeGate)], cancellationToken);
        bytesLeftBeforeGate -= read;
        return read;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => inner.WriteAsync(bytes, cancellationToken);

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

    public void Abort() => inner.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
