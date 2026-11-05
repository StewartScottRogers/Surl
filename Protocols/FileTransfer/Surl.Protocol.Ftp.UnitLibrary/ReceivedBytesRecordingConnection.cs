using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ftp;

/// <summary>
/// A control connection that hands every byte read from it to an upstream test case's
/// <see cref="FtpUpstreamTestCaseReplies"/> as it arrives, and is otherwise the connection it wraps.
/// </summary>
internal sealed class ReceivedBytesRecordingConnection(IConnection connection, FtpUpstreamTestCaseReplies replies) : IConnection
{
    public EndPoint LocalEndPoint => connection.LocalEndPoint;

    public EndPoint RemoteEndPoint => connection.RemoteEndPoint;

    public TlsSession? TlsSession => connection.TlsSession;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var count = await connection.ReadAsync(buffer, cancellationToken);
        replies.RecordReceived(buffer.Span[..count]);

        return count;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) =>
        connection.WriteAsync(bytes, cancellationToken);

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) =>
        connection.CompleteWritesAsync(cancellationToken);

    public void Abort() => connection.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) =>
        connection.UpgradeToTlsAsync(cancellationToken);

    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
