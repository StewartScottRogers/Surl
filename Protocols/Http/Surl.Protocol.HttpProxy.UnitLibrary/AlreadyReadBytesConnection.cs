using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// A connection whose first reads return the bytes <see cref="Surl.Http2.Http2ConnectionPreface"/>
/// read past the HTTP/2 preface, and whose later reads, and everything else, go to the
/// connection itself: what the HTTPS proxy hands <see cref="Surl.Http2.Http2ConnectionServer"/>.
/// </summary>
/// <param name="connection">The connection the bytes were read from.</param>
/// <param name="alreadyRead">The bytes already read, returned before any more is read.</param>
internal sealed class AlreadyReadBytesConnection(IConnection connection, ReadOnlyMemory<byte> alreadyRead) : IConnection
{
    private ReadOnlyMemory<byte> unreturned = alreadyRead;

    /// <inheritdoc />
    public EndPoint LocalEndPoint => connection.LocalEndPoint;

    /// <inheritdoc />
    public EndPoint RemoteEndPoint => connection.RemoteEndPoint;

    /// <inheritdoc />
    public TlsSession? TlsSession => connection.TlsSession;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (unreturned.IsEmpty)
        {
            return connection.ReadAsync(buffer, cancellationToken);
        }

        var count = Math.Min(unreturned.Length, buffer.Length);
        unreturned[..count].CopyTo(buffer);
        unreturned = unreturned[count..];

        return ValueTask.FromResult(count);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) =>
        connection.WriteAsync(bytes, cancellationToken);

    /// <inheritdoc />
    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) =>
        connection.CompleteWritesAsync(cancellationToken);

    /// <inheritdoc />
    public void Abort() => connection.Abort();

    /// <inheritdoc />
    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) =>
        connection.UpgradeToTlsAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
