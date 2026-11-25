using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// The MQTT 3.1.1 server: accepts the <c>CONNECT</c> upstream curl sends, keeps the message
/// of a <c>PUBLISH</c> with <c>RETAIN</c> set (or on a <c>--topic ...;retain</c> topic) as its topic's retained message, and answers a
/// <c>SUBSCRIBE</c> (curl's plain fetch) with the retained messages its filters match; given a
/// hold count, it then keeps the subscription open and sends later publishes.
/// ADR-0014 records the answers, ADR-0176 the retention rule.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is retained and what a subscriber receives.</b> A <c>PUBLISH</c> with <c>RETAIN</c> 1 is kept, per
/// topic, replaces the topic's retained message, and with an empty payload removes it; one with
/// <c>RETAIN</c> 0 (all upstream curl 8.21.0 sends, fixed header <c>0x30</c>) is delivered to live bus
/// subscribers but is not kept and neither replaces nor removes a retained message (MQTT 3.1.1
/// section 3.3.1.3, ADR-0176). A topic a <c>--topic &lt;filter&gt;;retain</c> declaration covers is kept
/// whatever the flag says. The
/// messages are kept by the <see cref="MessageBus.MessageBus"/> the server was given - one per
/// process, so every connection it answers, over <c>mqtt</c> or <c>mqtts</c>, and every other
/// messaging server on the bus shares them (ADR-0100, decision 3). A <c>SUBSCRIBE</c> is answered <c>SUBACK</c>,
/// granting QoS 0 to each valid topic filter and failure (<c>0x80</c>) to each invalid one;
/// then one QoS 0 <c>PUBLISH</c>, <c>RETAIN</c> set, for every kept message a valid filter
/// matches, in ordinal order of the bus's topic; then, without a hold count, <c>DISCONNECT</c>, and
/// the connection is closed, so a subscriber is not kept waiting for a future publish.
/// The <c>DISCONNECT</c> (server to client, which section 3.14 does not define) is sent because upstream
/// curl 8.21.0 ends its fetch and exits 0 on it and exits 56 on a bare close (ADR-0014, ADR-0176).
/// </para>
/// <para>
/// <b>Held subscription</b> (ADR-0014 Amendment 1), only when the server is given a hold count.
/// The server subscribes on the bus before it writes <c>SUBACK</c>, sends the retained messages,
/// and keeps the connection open. Each later publish a filter matches, from any protocol on the
/// bus and the subscriber's own included, is sent once per connection however many filters
/// match: QoS 0, <c>RETAIN</c> and <c>DUP</c> clear, under the name the scheme gives its topic; one
/// the scheme cannot name, or one already expired, is neither sent nor counted. Right after the
/// count-th <c>PUBLISH</c> sent on the connection, retained and live alike, the server sends
/// <c>DISCONNECT</c> and closes; a count of 0 never ends the subscription this way. While it is
/// held, the client's packets go on being read: <c>PINGREQ</c> gets <c>PINGRESP</c>, another
/// <c>SUBSCRIBE</c> adds filters (the same filter again stays one subscription) and sends their
/// retained messages against the same count, <c>UNSUBSCRIBE</c> removes filters and the connection
/// stays open, <c>PUBLISH</c> is handled as ever, and the client's <c>DISCONNECT</c> or close ends
/// it. A first <c>SUBSCRIBE</c> whose every filter is refused is answered <c>SUBACK</c> and
/// <c>DISCONNECT</c>. At most 1000 publishes and 16 MiB of payload wait to be sent; one more
/// closes the connection with no bytes and a log note (a command-stage delay rule's hold ends
/// first). The MQTT Keep Alive is not enforced; the
/// exchange ends otherwise only when its cancellation token does (the idle timeout, the maximum
/// exchange duration, or the server stopping).
/// </para>
/// <para>
/// <b>Topic names.</b> Every topic name and filter goes through the bus's
/// <see cref="MessageBus.TopicNameLookup"/> (<see cref="MessageBus.MessageBus.Names"/>) for the
/// listener's scheme: an MQTT name is the bus's topic as it stands, except where a
/// <c>--topic-map</c> names <c>mqtt</c> or <c>mqtts</c> on its left (ADR-0102 decision 3.3), which
/// covers both schemes. A <c>PUBLISH</c> on an aliased name is published on the alias's target. A
/// filter that is exactly an aliased name subscribes to the target; a wildcard filter is taken as it
/// stands. A retained message is sent under the aliased name when a map targets its topic, and is
/// not sent when its topic is spelled as an aliased name that means another topic, because no name
/// of the scheme reaches it.
/// </para>
/// <para>
/// <b>Connect.</b> The first packet must be <c>CONNECT</c>. <c>MQTT</c> at a level other
/// than 4, or <c>MQIsdp</c>, is answered <c>CONNACK</c> 1 (unacceptable protocol version) and
/// the connection is closed (section 3.2.2.3). At level 4, with connect flags section 3.1.2
/// allows, the server reads the client identifier, the will topic and message (skipped: no
/// will is kept, and no session), and the user name and password the flags announce; any of
/// them cut short is malformed. An empty client identifier without <c>CleanSession</c> is
/// answered <c>CONNACK</c> 2 and closed. Otherwise the user name, the password bytes and the
/// connection's <see cref="IConnection.TlsSession"/> (<see langword="null"/> over
/// <c>mqtt</c>) go to the <see cref="IAuthenticationPolicy"/>, and its verdict is answered as
/// ADR-0032 decision 5 says: accepted, <c>CONNACK</c> 0 with no session present; refused
/// credentials, <c>CONNACK</c> 4 (bad user name or password); refused as anonymous or as a
/// password over an unencrypted connection, <c>CONNACK</c> 5 (not authorized). After any
/// <c>CONNACK</c> other than 0 the connection is closed. Upstream curl 8.21.0 reports
/// <c>CONNACK</c> 4 and 5 alike, as exit 8, "Expected 0000 but got 000n".
/// </para>
/// <para>
/// <b>Other packets.</b> A QoS 1 publish is answered <c>PUBACK</c>, a QoS 2 publish
/// <c>PUBREC</c>, and <c>PUBREL</c> <c>PUBCOMP</c>; the message is handled on arrival, whatever
/// its QoS. <c>UNSUBSCRIBE</c> (which ends a held subscription's filters) is answered <c>UNSUBACK</c>, <c>PINGREQ</c> <c>PINGRESP</c>,
/// and <c>DISCONNECT</c> by closing the connection.
/// </para>
/// <para>
/// <b>Violations and limits.</b> A malformed packet, a packet only a server sends, a packet
/// of reserved type 0 or 15, wrong fixed header flags, a first packet other than
/// <c>CONNECT</c> and a second <c>CONNECT</c> close the connection with no reply, as MQTT
/// 3.1.1 section 4.8 says; so does a <c>PUBLISH</c> the bus refuses (past
/// <see cref="MessageBus.MessageBus.MaxTopics"/>, <see cref="MessageBus.MessageBus.MaxRetainedPayloadBytes"/>
/// or another of its bounds, or a topic holding U+0000, which section 1.5.3 forbids); so do a remaining
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
public sealed class MqttProtocolServer : IConnectionProtocolServer, IExchangeBytesRedactingServer
{
    private readonly MessageBus.MessageBus bus;
    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly int? holdSubscriptionCount;

    /// <summary>
    /// Creates an MQTT server that publishes every message to <paramref name="bus"/>, answers a
    /// subscribe with the bus's retained messages, and asks
    /// <paramref name="authenticationPolicy"/> whether to accept each <c>CONNECT</c>'s user name
    /// and password (ADR-0032, decision 5).
    /// </summary>
    /// <param name="bus">The process's one message bus, shared with every other messaging server.</param>
    /// <param name="authenticationPolicy">Judges the login of every <c>CONNECT</c>.</param>
    /// <param name="holdSubscriptionCount">
    /// <see langword="null"/>, the default, ends every subscribe with <c>DISCONNECT</c> after the
    /// retained messages. A number holds each subscription open and sends later publishes, ending
    /// it with <c>DISCONNECT</c> after that many <c>PUBLISH</c> packets; 0 sets no count
    /// (ADR-0014 Amendment 1).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="holdSubscriptionCount"/> is negative.</exception>
    public MqttProtocolServer(MessageBus.MessageBus bus, IAuthenticationPolicy authenticationPolicy, int? holdSubscriptionCount = null)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);
        ArgumentOutOfRangeException.ThrowIfNegative(holdSubscriptionCount.GetValueOrDefault(), nameof(holdSubscriptionCount));

        this.bus = bus;
        this.authenticationPolicy = authenticationPolicy;
        this.holdSubscriptionCount = holdSubscriptionCount;
    }

    /// <summary>
    /// The schemes answered: <c>mqtt</c>, and <c>mqtts</c>, the same exchange over implicit TLS.
    /// The engine performs the handshake and hands over a connection that already carries
    /// plaintext (ADR-0010), so both are answered alike (ADR-0014).
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["mqtt", "mqtts"]);

    /// <summary>
    /// Creates the redactor that logs a <c>CONNECT</c>'s password as <c>****</c> and every other
    /// byte as it arrived (ADR-0182, decision 6).
    /// </summary>
    /// <param name="context">The exchange the redactor is for.</param>
    /// <returns>A redactor for the connection, used by that exchange alone.</returns>
    public IReceivedBytesRedactor CreateRedactor(ExchangeRedactionContext context) => new MqttConnectBytesRedactor();

    /// <summary>
    /// Answers every packet on <paramref name="connection"/> until the client disconnects or
    /// closes it, a packet is refused, or a subscribe ends: once answered without a hold count,
    /// and with one at its publish count or past its backlog bounds.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var reader = new MqttPacketReader(connection, context.Limits.MaxMessageBytes, context.Limits.MaxUploadBytes);
        await using var responder = new MqttPacketResponder(connection, context, bus, authenticationPolicy, holdSubscriptionCount);

        try
        {
            await AnswerPacketsAsync(reader, responder, context);
        }
        catch (OperationCanceledException) when (responder.HasHeldBacklogOverflowed && !context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note(
                $"More than {MqttHeldSubscription.MaxWaitingPublishes} publishes or {MqttHeldSubscription.MaxWaitingPayloadBytes} payload bytes waited for the held subscription; closed with no reply.");
        }
    }

    private static async Task AnswerPacketsAsync(MqttPacketReader reader, MqttPacketResponder responder, ExchangeContext context)
    {
        MqttPacketReadResult? result = await ReadFirstPacketAsync(reader, context);
        while (result?.Packet is { } packet && await responder.AnswerAsync(packet))
        {
            result = await responder.ReadNextPacketAsync(reader);
        }

        // No result: a held subscription reached its count, and has noted it.
        if (result is not null)
        {
            NoteNoPacket(context, result.Outcome);
        }
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
