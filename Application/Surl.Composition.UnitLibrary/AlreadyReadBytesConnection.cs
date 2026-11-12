using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// A connection whose first reads return bytes already read from it - those
/// <c>Http2ConnectionPreface</c> read to tell an HTTP/1.x request from the HTTP/2 preface,
/// or those it read past the preface -
/// and whose later reads, and everything else, go to the connection itself.
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
        if (unreturned.IsEmpty || buffer.IsEmpty)
        {
            return connection.ReadAsync(buffer, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
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
