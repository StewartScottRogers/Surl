using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// A connection over an <see cref="InMemoryConnection"/> whose writes after <c>connection.open-ok</c>
/// never complete until they are cancelled - a farewell stuck in a full send buffer.
/// </summary>
internal sealed class UnitTestOpenOkThenStallingConnection(InMemoryConnection inner, int bytesBeforeStalling) : IConnection
{
    private volatile bool stalled;

    public bool Stalled => stalled;

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (inner.WrittenBytes.Length < bytesBeforeStalling)
        {
            await inner.WriteAsync(bytes, cancellationToken);
            return;
        }

        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var registration = cancellationToken.Register(() => never.TrySetCanceled(cancellationToken));
        stalled = true;
        await never.Task;
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

    public void Abort() => inner.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
