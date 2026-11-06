using System.Net;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// A connection whose reads go through the <see cref="HttpConnectionReader"/> that read its
/// HTTP/1.x request heads - so the bytes the reader already holds past the last head come first -
/// and whose writes, and everything else, go to the connection itself. After an
/// <c>Upgrade: h2c</c>'s <c>101</c> the HTTP/2 server reads through it (ADR-0091).
/// </summary>
/// <param name="connection">The connection.</param>
/// <param name="reader">The connection's HTTP/1.x reader.</param>
internal sealed class HttpConnectionReaderConnection(IConnection connection, HttpConnectionReader reader) : IConnection
{
    /// <inheritdoc />
    public EndPoint LocalEndPoint => connection.LocalEndPoint;

    /// <inheritdoc />
    public EndPoint RemoteEndPoint => connection.RemoteEndPoint;

    /// <inheritdoc />
    public TlsSession? TlsSession => connection.TlsSession;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        reader.ReadAsync(buffer, cancellationToken);

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
