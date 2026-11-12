using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// A connection that hands every byte read from it to an upstream test case's
/// <see cref="HttpProxyUpstreamTestCaseReplies"/> as it arrives, until <see cref="StopRecording"/>, and
/// is otherwise the connection it wraps. Recording stops when a tunnel opens: a tunnel carries the origin's traffic, which the case's
/// <c>&lt;protocol&gt;</c> checks, not its <c>&lt;proxy&gt;</c>. Another connection from curl is recorded
/// from its first byte.
/// </summary>
internal sealed class HttpProxyUpstreamTestCaseRecordingConnection(IConnection connection, HttpProxyUpstreamTestCaseReplies replies) : IConnection
{
    private volatile bool isRecording = true;

    public void StopRecording() => isRecording = false;

    public EndPoint LocalEndPoint => connection.LocalEndPoint;

    public EndPoint RemoteEndPoint => connection.RemoteEndPoint;

    public TlsSession? TlsSession => connection.TlsSession;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var count = await connection.ReadAsync(buffer, cancellationToken);
        if (isRecording)
        {
            replies.RecordReceived(buffer.Span[..count]);
        }

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
