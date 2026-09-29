using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// The MQTT 3.1.1 server: accepts the <c>CONNECT</c> upstream curl sends, keeps the message
/// of every <c>PUBLISH</c> (curl's <c>-d</c>) as its topic's retained message, and answers a
/// <c>SUBSCRIBE</c> (curl's plain fetch) with the retained messages its filters match.
/// ADR-0014 records the answers.
/// </summary>
/// <remarks>
/// <para>
/// <b>What a subscriber receives.</b> Every message published to the server is kept, per
/// topic, whether or not its <c>RETAIN</c> flag was set: upstream curl 8.21.0 publishes with
/// <c>RETAIN</c> clear, and without this a curl fetch after a curl publish would receive
/// nothing. A later publish to the topic replaces it, and an empty payload removes it. The
/// messages live as long as the <see cref="MqttRetainedMessages"/> the server was given, so
/// every connection it answers shares them. A <c>SUBSCRIBE</c> is answered <c>SUBACK</c>,
/// granting QoS 0 to each valid topic filter and failure (<c>0x80</c>) to each invalid one;
/// then one QoS 0 <c>PUBLISH</c>, <c>RETAIN</c> set, for every kept message a valid filter
/// matches, in ordinal order of topic; then <c>DISCONNECT</c>, and the connection is closed.
/// The <c>DISCONNECT</c> is what makes upstream curl end its fetch and exit 0; a bare close
/// makes it exit 56. A subscriber is never kept waiting for a future publish.
/// </para>
/// <para>
/// <b>Connect.</b> The first packet must be <c>CONNECT</c>. Protocol name <c>MQTT</c> at
/// level 4 with connect flags section 3.1.2 allows is answered <c>CONNACK</c> 0 with no
/// session present, whatever client identifier, will, user name or password it carries (none
/// of them is read); <c>MQTT</c> at another level, or <c>MQIsdp</c>, is
/// answered <c>CONNACK</c> 1 (unacceptable protocol version) and the connection is closed
/// (section 3.2.2.3); an empty client identifier without <c>CleanSession</c> is answered
/// <c>CONNACK</c> 2 and closed.
/// </para>
/// <para>
/// <b>Other packets.</b> A QoS 1 publish is answered <c>PUBACK</c>, a QoS 2 publish
/// <c>PUBREC</c>, and <c>PUBREL</c> <c>PUBCOMP</c>; the message is kept on arrival, whatever
/// its QoS. <c>UNSUBSCRIBE</c> is answered <c>UNSUBACK</c>, <c>PINGREQ</c> <c>PINGRESP</c>,
/// and <c>DISCONNECT</c> by closing the connection.
/// </para>
/// <para>
/// <b>Violations and limits.</b> A malformed packet, a packet only a server sends, a packet
/// of reserved type 0 or 15, wrong fixed header flags, a first packet other than
/// <c>CONNECT</c> and a second <c>CONNECT</c> close the connection with no reply, as MQTT
/// 3.1.1 section 4.8 says; so does a <c>PUBLISH</c> the retained messages have no room for
/// (<see cref="MqttRetainedMessages.MaxTopics"/>,
/// <see cref="MqttRetainedMessages.MaxTotalPayloadBytes"/>); so do a remaining
/// length whose fourth byte has its continuation bit set, and a packet whose fixed header
/// announces more than <see cref="ExchangeLimits.MaxMessageBytes"/> bytes, fixed header
/// included, which is refused before any of its body is read (ADR-0006, sections 1 and 5);
/// so does a <c>PUBLISH</c> whose payload is longer than
/// <see cref="ExchangeLimits.MaxUploadBytes"/>, refused once the two bytes of its topic
/// name's length are read and before any more of it is; and so does a first packet
/// (<c>CONNECT</c>) not complete within <see cref="ExchangeLimits.HeadTimeout"/> of the
/// start of <see cref="ServeAsync"/>. Once it is complete, the head timeout no longer
/// applies: a quiet client is bounded only by <c>Surl.Core</c>'s idle timeout. MQTT 3.1.1
/// has no packet a server sends to say why, so each limit closes with no bytes, and a
/// connection past a connection limit is closed bare too: there is no
/// <see cref="IConnectionRefusalWriter"/> (ADR-0006, section 5). Upstream curl 8.21.0
/// reports a close before <c>CONNACK</c> as exit 56, "Connection disconnected", and a close
/// after the <c>PUBLISH</c> of <c>-d</c> not at all: it exits 0 without waiting for a reply.
/// A client that closes part way through a packet gets no reply. Every close is noted in
/// the exchange log.
/// </para>
/// </remarks>
public sealed class MqttProtocolServer : IConnectionProtocolServer
{
    private readonly MqttRetainedMessages retainedMessages;

    /// <summary>
    /// Creates an MQTT server that keeps published messages in <paramref name="retainedMessages"/>.
    /// </summary>
    /// <param name="retainedMessages">The messages publishes keep and subscribes receive.</param>
    public MqttProtocolServer(MqttRetainedMessages retainedMessages)
    {
        ArgumentNullException.ThrowIfNull(retainedMessages);

        this.retainedMessages = retainedMessages;
    }

    /// <summary>
    /// The one scheme answered: <c>mqtt</c>.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["mqtt"]);

    /// <summary>
    /// Answers every packet on <paramref name="connection"/> until the client disconnects or
    /// closes it, a subscribe has been answered, or a packet is refused.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var reader = new MqttPacketReader(connection, context.Limits.MaxMessageBytes, context.Limits.MaxUploadBytes);
        var responder = new MqttPacketResponder(connection, context, retainedMessages);

        var result = await ReadFirstPacketAsync(reader, context);
        while (result.Packet is { } packet && await responder.AnswerAsync(packet))
        {
            result = await reader.ReadPacketAsync(context.CancellationToken);
        }

        NoteNoPacket(context, result.Outcome);
    }

    // The head timeout runs from here until the first packet is complete; later packets are
    // bounded only by Surl.Core's idle timeout (ADR-0006, section 1).
    private static async ValueTask<MqttPacketReadResult> ReadFirstPacketAsync(MqttPacketReader reader, ExchangeContext context)
    {
        if (context.Limits.HeadTimeout == Timeout.InfiniteTimeSpan)
        {
            return await reader.ReadPacketAsync(context.CancellationToken);
        }

        using var headTimeout = new CancellationTokenSource(context.Limits.HeadTimeout, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headTimeout.Token);
        try
        {
            return await reader.ReadPacketAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            return MqttPacketReadResult.NoPacket(MqttPacketReadOutcome.HeadTimedOut);
        }
    }

    private static void NoteNoPacket(ExchangeContext context, MqttPacketReadOutcome outcome)
    {
        switch (outcome)
        {
            case MqttPacketReadOutcome.ConnectionClosedMidPacket:
                context.Log.Note("The client closed the connection part way through a packet.");
                break;
            case MqttPacketReadOutcome.MalformedRemainingLength:
                context.Log.Note("A remaining length ran past four bytes (MQTT 3.1.1, section 2.2.3); closed with no reply.");
                break;
            case MqttPacketReadOutcome.PacketTooLarge:
                context.Log.Note($"A packet was longer than {context.Limits.MaxMessageBytes} bytes; closed with no reply.");
                break;
            case MqttPacketReadOutcome.PublishPayloadTooLarge:
                context.Log.Note($"A PUBLISH payload was longer than {context.Limits.MaxUploadBytes} bytes; closed with no reply.");
                break;
            case MqttPacketReadOutcome.HeadTimedOut:
                context.Log.Note("The first packet was not complete within the head timeout; closed with no reply.");
                break;
        }
    }
}
