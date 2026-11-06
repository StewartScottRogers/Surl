using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// A connection that cuts the bytes crossing it each way into whole MQTT packets, logs each in an
/// upstream test case's <see cref="MqttUpstreamTestCaseReplies"/>, and hands each client
/// <c>SUBSCRIBE</c>'s remaining bytes to <paramref name="subscribeRead"/> before the server reads
/// it; otherwise it is the connection it wraps. After a remaining length whose fourth byte has its
/// continuation bit set, a direction's bytes are passed on unlogged.
/// </summary>
/// <param name="connection">The connection wrapped.</param>
/// <param name="replies">Where the packets are logged.</param>
/// <param name="subscribeRead">Called with the remaining bytes of each whole <c>SUBSCRIBE</c> the client sent.</param>
internal sealed class MqttPacketLoggingConnection(
    IConnection connection, MqttUpstreamTestCaseReplies replies, Action<byte[]> subscribeRead) : IConnection
{
    private readonly MqttPacketCutter fromClient = new();
    private readonly MqttPacketCutter fromServer = new();

    public EndPoint LocalEndPoint => connection.LocalEndPoint;

    public EndPoint RemoteEndPoint => connection.RemoteEndPoint;

    public TlsSession? TlsSession => connection.TlsSession;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var count = await connection.ReadAsync(buffer, cancellationToken);
        foreach (var (packet, headerLength) in fromClient.Cut(buffer.Span[..count]))
        {
            replies.RecordPacket(fromClient: true, packet, headerLength);
            if (packet[0] >> 4 == (int)MqttPacketType.Subscribe)
            {
                subscribeRead(packet[headerLength..]);
            }
        }

        return count;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        foreach (var (packet, headerLength) in fromServer.Cut(bytes.Span))
        {
            replies.RecordPacket(fromClient: false, packet, headerLength);
        }

        return connection.WriteAsync(bytes, cancellationToken);
    }

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => connection.CompleteWritesAsync(cancellationToken);

    public void Abort() => connection.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => connection.UpgradeToTlsAsync(cancellationToken);

    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
