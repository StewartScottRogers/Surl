using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// An <see cref="IConnection"/> that passes every call to <paramref name="connection"/> but
/// throws <see cref="IOException"/> when disposed, as a transport that fails to close would.
/// </summary>
internal sealed class DisposeFailingConnection(IConnection connection) : IConnection
{
    /// <summary>
    /// Whether <see cref="DisposeAsync"/> has been called.
    /// </summary>
    public bool DisposeCalled { get; private set; }

    public EndPoint LocalEndPoint => connection.LocalEndPoint;

    public EndPoint RemoteEndPoint => connection.RemoteEndPoint;

    public TlsSession? TlsSession => connection.TlsSession;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        connection.ReadAsync(buffer, cancellationToken);

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) =>
        connection.WriteAsync(bytes, cancellationToken);

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) =>
        connection.CompleteWritesAsync(cancellationToken);

    public void Abort() => connection.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) =>
        connection.UpgradeToTlsAsync(cancellationToken);

    public ValueTask DisposeAsync()
    {
        DisposeCalled = true;
        throw new IOException("The transport failed to close.");
    }
}
