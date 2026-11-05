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

        return new MqttProtocolServer(bus, authenticationPolicy).ServeAsync(logged, context);
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
