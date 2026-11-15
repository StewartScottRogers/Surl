using System.Buffers.Binary;
using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// The MQTT server half of an upstream curl test case (ADR-0080 decision 2): Surl's own MQTT
/// server, <see cref="MqttProtocolServer"/>, reading every packet and answering with its own
/// <c>CONNACK</c>, <c>SUBACK</c>, <c>PUBLISH</c> and <c>DISCONNECT</c>, while the case's
/// <see cref="MqttUpstreamTestCaseReplies"/> supplies the payload a subscriber receives.
/// </summary>
/// <remarks>
/// <para>
/// Each connection gets a message bus of its own. When the client's <c>SUBSCRIBE</c> arrives,
/// the case's <c>&lt;data&gt;</c> is published, retained, to each topic it names, before Surl's
/// server reads it; Surl's server then answers the subscription as it answers any. Every packet
/// each way is logged. Logins go to the authentication policy, which the test project makes
/// accept everyone.
/// </para>
/// <para>
/// Never composed by the shipped <c>surl</c>: <c>Surl.Conformance.UnitTests</c> composes it on
/// the loopback listener for upstream's test cases.
/// </para>
/// </remarks>
public sealed class MqttUpstreamTestCaseServer : IConnectionProtocolServer
{
    private readonly MqttUpstreamTestCaseReplies replies;
    private readonly IAuthenticationPolicy authenticationPolicy;

    /// <summary>
    /// Creates the server half of one case.
    /// </summary>
    /// <param name="replies">The case's reply and packet log.</param>
    /// <param name="authenticationPolicy">Who may connect: for upstream's cases, everyone.</param>
    public MqttUpstreamTestCaseServer(MqttUpstreamTestCaseReplies replies, IAuthenticationPolicy authenticationPolicy)
    {
        ArgumentNullException.ThrowIfNull(replies);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);

        this.replies = replies;
        this.authenticationPolicy = authenticationPolicy;
    }

    /// <summary>
    /// The schemes answered: <c>mqtt</c>, and <c>mqtts</c>, whose TLS handshake the engine
    /// completes before <see cref="ServeAsync"/>.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["mqtt", "mqtts"]);

    /// <summary>
    /// Serves <paramref name="connection"/> with Surl's MQTT server, logging every packet.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var bus = new MessageBus.MessageBus(context.TimeProvider);
        var logged = new MqttPacketLoggingConnection(connection, replies, remaining => PublishToEachTopic(bus, remaining));

        if (replies.ConnectAnswer is { } answer)
        {
            return AnswerConnectWithAsync(connection, logged, answer, context.CancellationToken);
        }

        return replies.ScriptsSubscribe
            ? AnswerSubscribeWithScriptAsync(connection, context.CancellationToken)
            : new MqttProtocolServer(bus, authenticationPolicy).ServeAsync(logged, context);
    }

    /// <summary>
    /// Answers <c>CONNECT</c> as normal, then the first <c>SUBSCRIBE</c> with the packets the case's
    /// <c>&lt;servercmd&gt;</c> lines script (ADR-0155 decisions 4 to 7), and ends the connection.
    /// </summary>
    private async Task AnswerSubscribeWithScriptAsync(IConnection connection, CancellationToken cancellationToken)
    {
        var subscribe = Array.Empty<byte>();
        var logged = new MqttPacketLoggingConnection(connection, replies, remaining => subscribe = remaining);
        var buffer = new byte[4096];
        if (await logged.ReadAsync(buffer, cancellationToken) == 0)
        {
            return;
        }

        await logged.WriteAsync(new byte[] { 0x20, 0x02, 0x00, 0x00 }, cancellationToken);
        if (await logged.ReadAsync(buffer, cancellationToken) == 0 || subscribe.Length < 4)
        {
            return;
        }

        var topicLength = Math.Min(BinaryPrimitives.ReadUInt16BigEndian(subscribe.AsSpan(2)), subscribe.Length - 4);
        var body = new byte[2 + topicLength + replies.Data.Length];
        BinaryPrimitives.WriteUInt16BigEndian(body, (ushort)topicLength);
        subscribe.AsSpan(4, topicLength).CopyTo(body.AsSpan(2));
        replies.Data.CopyTo(body, 2 + topicLength);
        var publish = new byte[] { 0x30, (byte)body.Length }.Concat(body).ToArray();
        var suback = new byte[] { 0x90, 0x03, subscribe[0], subscribe[1], 0x00 };
        var disconnect = new byte[] { 0xE0, 0x00 };

        if (replies.PublishBeforeSuback)
        {
            await WriteBeforeSubackAsync(connection, logged, publish, suback, disconnect, cancellationToken);
        }
        else
        {
            await logged.WriteAsync(suback, cancellationToken);
            if (replies.ExcessiveRemaining)
            {
                await WriteLoggedAsAsync(connection, "PUBLISH", body.Length, new byte[] { 0x30, 0xFF, 0xFF, 0xFF, 0x80 }.Concat(body).ToArray(), cancellationToken);
                await logged.WriteAsync(disconnect, cancellationToken);
            }
            else
            {
                await logged.WriteAsync(publish, cancellationToken);
                await WriteLoggedAsAsync(connection, "DISCONNECT-malformed", 2, [0xE0, 0x02, 0x00, 0x00], cancellationToken);
            }
        }

        await connection.CompleteWritesAsync(cancellationToken);
    }

    private async Task WriteBeforeSubackAsync(IConnection connection, IConnection logged, byte[] publish, byte[] suback, byte[] disconnect, CancellationToken cancellationToken)
    {
        if (replies.ShortPublish)
        {
            await WriteLoggedAsAsync(connection, "PUBLISH", publish.Length - 2, publish[..^2], cancellationToken);
            return;
        }

        await logged.WriteAsync(publish, cancellationToken);
        await logged.WriteAsync(suback, cancellationToken);
        await logged.WriteAsync(disconnect, cancellationToken);
    }

    private async Task WriteLoggedAsAsync(IConnection connection, string name, int remainingLength, byte[] bytes, CancellationToken cancellationToken)
    {
        await connection.WriteAsync(bytes, cancellationToken);
        replies.RecordAnswer(new ConnectAnswer(name, remainingLength, bytes));
    }

    /// <summary>
    /// Reads the client's <c>CONNECT</c> (logged), writes <paramref name="answer"/> in place of a
    /// <c>CONNACK</c> straight to the connection, logs it as the case expects, and ends the
    /// connection (ADR-0155 decisions 1 to 3).
    /// </summary>
    private async Task AnswerConnectWithAsync(IConnection connection, IConnection logged, ConnectAnswer answer, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        if (await logged.ReadAsync(buffer, cancellationToken) == 0)
        {
            return;
        }

        await connection.WriteAsync(answer.Bytes, cancellationToken);
        replies.RecordAnswer(answer);
        await connection.CompleteWritesAsync(cancellationToken);
    }

    /// <summary>
    /// Publishes the case's data, retained, to each topic filter of a <c>SUBSCRIBE</c>'s remaining
    /// bytes: a packet identifier, then each filter as a two-byte length, its UTF-8 and a QoS byte.
    /// A filter cut short ends the list, and a filter the bus refuses is passed over; Surl's server
    /// answers either itself.
    /// </summary>
    private void PublishToEachTopic(MessageBus.MessageBus bus, byte[] remaining)
    {
        var index = 2;
        while (index + 2 <= remaining.Length)
        {
            var length = BinaryPrimitives.ReadUInt16BigEndian(remaining.AsSpan(index));
            if (index + 2 + length > remaining.Length)
            {
                return;
            }

            bus.Publish(new BusMessage(Encoding.UTF8.GetString(remaining, index + 2, length), replies.Data) { Retain = true });
            index += 2 + length + 1;
        }
    }
}
