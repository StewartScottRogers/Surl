using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// The MQTT 5.0 broker (ADR-0093): answers <c>CONNECT</c> with <c>CONNACK</c>, judges the login or
/// runs enhanced authentication, keeps the client's session across connections, holds the client
/// to its Keep Alive, and publishes its Will when the connection ends without a normal
/// <c>DISCONNECT</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Before CONNACK</b> (ADR-0093 decisions 1 and 6). The first packet must be <c>CONNECT</c>; any
/// other, bytes that are not a readable fixed header, a protocol name that is not MQTT, and a first
/// packet not complete within <see cref="ExchangeLimits.HeadTimeout"/> close the connection with no
/// bytes. A <c>CONNECT</c> that is refused is answered <c>CONNACK</c> with Session Present 0 and its
/// reason code, then closed: <c>0x84</c> for <c>MQTT</c> or <c>MQIsdp</c> at a level other than 5,
/// <c>0x81</c> for a malformed one, <c>0x82</c> for a property twice or a value MQTT 5.0 forbids,
/// <c>0x95</c> for one over <see cref="ExchangeLimits.MaxMessageBytes"/>, and <c>0x97</c> for a new
/// session past the sessions kept with no connection. Without an Authentication Method, the user
/// name, the password and the connection's <see cref="IConnection.TlsSession"/> go to the
/// <see cref="IAuthenticationPolicy"/>: accepted is <c>0x00</c>; refused credentials <c>0x86</c>;
/// no user name, or a password over a connection with no TLS session, <c>0x87</c> (ADR-0093
/// decision 2).
/// </para>
/// <para>
/// <b>Enhanced authentication</b> (section 4.12, ADR-0093 decision 2). A <c>CONNECT</c> with an
/// Authentication Method is judged by <see cref="Mqtt5EnhancedAuthentication"/> alone: the method
/// must be <c>SCRAM-SHA-256</c> or <c>SCRAM-SHA-1</c> and offered by the
/// <see cref="ISaslAuthenticationPolicy"/>, otherwise <c>CONNACK</c> <c>0x8C</c>. Each challenge
/// goes in <c>AUTH</c> <c>0x18</c> with the method and its data; the client must answer
/// <c>AUTH</c> <c>0x18</c> with the same method, and any other packet is <c>CONNACK</c>
/// <c>0x82</c> (a refused packet its own code). Success is <c>CONNACK</c> <c>0x00</c> carrying the
/// method and the method's last message; failure <c>0x87</c>. A <c>DISCONNECT</c>, a close or the
/// head timeout during the exchange ends it with no <c>CONNACK</c>. After such a <c>CONNACK</c>,
/// <c>AUTH</c> <c>0x19</c> with the same method re-authenticates while other packets keep flowing:
/// challenges again in <c>AUTH</c> <c>0x18</c>, success <c>AUTH</c> <c>0x00</c>, and failure, or
/// success for another account, <c>DISCONNECT</c> <c>0x87</c>. Any other <c>AUTH</c> is
/// <c>DISCONNECT</c> <c>0x82</c>.
/// </para>
/// <para>
/// <b>Sessions</b> (section 4.1, ADR-0093 decision 3). A session - subscriptions, waiting messages,
/// unacknowledged <c>PUBLISH</c>es sent and QoS 2 Packet Identifiers awaiting <c>PUBREL</c> - is
/// kept after its connection for its Session Expiry Interval, in memory. A <c>CONNECT</c> with Clean
/// Start 0 for the same identifier and account resumes it with Session Present 1, resending its
/// unacknowledged <c>PUBLISH</c>es with DUP 1 and its <c>PUBREL</c>s in their original order, then
/// its waiting messages; Clean Start 1 discards it. While no connection holds it, QoS 1 and 2
/// messages wait for it, up to 1000 messages and 16 MiB of payload.
/// </para>
/// <para>
/// <b>Session takeover</b> (<c>[MQTT-3.1.4-3]</c>, ADR-0093 decision 1). A <c>CONNECT</c> for an
/// identifier already connected sends the older connection <c>DISCONNECT</c> <c>0x8E</c> and closes
/// it; that connection's session and Will are settled as for any other close, and only then is the
/// new <c>CONNECT</c>'s session opened, so Clean Start 0 resumes the session the older connection
/// kept and Clean Start 1 discards it. The older connection's Will is therefore published at once
/// when its Will Delay Interval or Session Expiry Interval is 0 or the new Clean Start is 1, and not
/// when Clean Start 0 resumes the session before the Will Delay passes. The wait for the older
/// connection runs within the new connection's head timeout; past it the new one closes with no reply.
/// </para>
/// <para>
/// <b>The CONNACK of an accepted CONNECT</b> carries Session Present and, in this order: Session
/// Expiry Interval <see cref="MaxSessionExpirySeconds"/> when the client asked for more; Receive
/// Maximum <see cref="ReceiveMaximum"/>; Maximum Packet Size
/// <see cref="ExchangeLimits.MaxMessageBytes"/> unless it is 0; the Assigned Client Identifier
/// when the client sent a zero-length one; Topic Alias Maximum <see cref="TopicAliasMaximum"/>;
/// and Server Keep Alive <see cref="MaxKeepAliveSeconds"/> when the client's Keep Alive was 0 or
/// above it.
/// </para>
/// <para>
/// <b>After CONNACK.</b> <c>PINGREQ</c> is answered <c>PINGRESP</c>. A client <c>DISCONNECT</c>
/// closes the connection: <c>0x00</c> discards the Will, any other reason code, <c>0x04</c>
/// Disconnect with Will Message among them, keeps it. A Session Expiry Interval on it replaces the
/// session's, capped at <see cref="MaxSessionExpirySeconds"/>, except that raising it from 0 is
/// <c>DISCONNECT</c> <c>0x82</c>. Silence for one and a half times the Keep Alive in force is
/// answered <c>DISCONNECT</c> <c>0x8D</c>. A second <c>CONNECT</c> and a packet only a server sends
/// are answered <c>DISCONNECT</c> <c>0x82</c>; a refused packet <c>DISCONNECT</c> with its fault's
/// reason code. Each of those <c>DISCONNECT</c>s closes the connection. When the server stops (the
/// exchange's cancellation token), each connection is sent <c>DISCONNECT</c> <c>0x8B</c> Server
/// shutting down, best effort within two seconds, before it closes.
/// </para>
/// <para>
/// <b>PUBLISH</b> (ADR-0093 decision 4, amended by ADR-0100 decision 3.9). Each is judged by
/// <see cref="Mqtt5InboundPublishes"/> - a Subscription Identifier or a Payload Format Indicator
/// above 1 <c>0x82</c>, a Response Topic holding a wildcard <c>0x82</c>, a Topic Alias of 0 or above <see cref="TopicAliasMaximum"/> <c>0x94</c>,
/// an empty Topic Name with no alias set <c>0x82</c>, a QoS 2 <c>PUBLISH</c> past
/// <see cref="ReceiveMaximum"/> awaiting <c>PUBREL</c> <c>0x93</c>, each a <c>DISCONNECT</c> -
/// then published on the shared message bus for the client's account. QoS 1 is answered
/// <c>PUBACK</c> and QoS 2 <c>PUBREC</c>: <c>0x00</c>, <c>0x10</c> when no subscription or queue
/// took it, <c>0x87</c> when topic access refuses it, <c>0x97</c> past a bus bound, <c>0x99</c>
/// for a payload its Payload Format Indicator mislabels (at QoS 0 a <c>DISCONNECT</c>
/// <c>0x99</c>); a payload larger than the bus takes is <c>DISCONNECT</c> <c>0x95</c>, and a
/// refused QoS 0 <c>PUBLISH</c> is dropped. A QoS 2 Packet Identifier is remembered until
/// <c>PUBREL</c>, which is answered <c>PUBCOMP</c> (<c>0x92</c> for one not remembered), and a
/// repeat of it before then is answered <c>PUBREC</c> again and not delivered again.
/// </para>
/// <para>
/// <b>SUBSCRIBE</b> (ADR-0093 decision 5): <see cref="Mqtt5ClientSubscriptions"/> makes each
/// Topic Filter a bus subscription granted its Maximum QoS (<c>$share/&lt;group&gt;/</c> a fan-out
/// group, one member of which gets each message), replacing one to an identical filter. <c>SUBACK</c>
/// answers each filter with the QoS granted, <c>0x8F</c> for a filter the bus cannot map, <c>0x87</c>
/// when topic access refuses it, <c>0x97</c> past the bus's subscriptions a connection, and every
/// filter <c>0x91</c> when the Packet Identifier awaits a <c>PUBREL</c>. A Subscription Identifier
/// of 0, or No Local on a shared subscription, is <c>DISCONNECT</c> <c>0x82</c>. No Local keeps the
/// client's own <c>PUBLISH</c>es from that subscription; Retain As Published keeps a live message's
/// RETAIN, which otherwise goes as 0; Retain Handling 0 sends the matching retained messages after
/// every <c>SUBACK</c>, 1 only for a new subscription, 2 never, and a shared subscription never, each
/// with RETAIN 1. A message matching several non-shared subscriptions goes once, at the highest QoS
/// granted, with every Subscription Identifier they carry. <c>UNSUBSCRIBE</c> is answered
/// <c>UNSUBACK</c>: <c>0x00</c> for a deleted subscription, <c>0x11</c> for none, <c>0x8F</c> for an
/// invalid filter, <c>0x91</c> as for <c>SUBSCRIBE</c>. <see cref="Mqtt5OutboundDeliveries"/> sends
/// each message at the lower of its QoS and the granted one, within the client's Receive Maximum,
/// with Topic Aliases when the client allows them and the Message Expiry Interval left, dropping one
/// that expired while it waited.
/// </para>
/// <para>
/// <b>The Will.</b> When a connection ends any other way than the client's <c>DISCONNECT</c>
/// <c>0x00</c>, its Will is published through <see cref="Mqtt5BrokerState"/> after the lesser of
/// its Will Delay Interval and the Session Expiry Interval in force (section 3.1.2.5), at once
/// when that is 0.
/// </para>
/// </remarks>
public sealed class Mqtt5Broker : IConnectionProtocolServer
{
    /// <summary>
    /// The longest Session Expiry Interval the broker grants, in seconds (ADR-0093 decision 1).
    /// </summary>
    public const uint MaxSessionExpirySeconds = 3600;

    /// <summary>
    /// The longest Keep Alive the broker lets stand, in seconds, and the Server Keep Alive it
    /// sends in place of a longer one or of 0 (ADR-0093 decision 1).
    /// </summary>
    public const ushort MaxKeepAliveSeconds = 60;

    /// <summary>
    /// The Receive Maximum every <c>CONNACK</c> carries (ADR-0093 decision 1).
    /// </summary>
    public const ushort ReceiveMaximum = 100;

    /// <summary>
    /// The Topic Alias Maximum every <c>CONNACK</c> carries (ADR-0093 decision 1).
    /// </summary>
    public const ushort TopicAliasMaximum = 10;

    private readonly Mqtt5BrokerState state;
    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly ISaslAuthenticationPolicy? saslAuthenticationPolicy;
    private readonly bool disconnectsAfterRetained;

    /// <summary>
    /// Creates a broker whose connections share <paramref name="state"/> and whose logins
    /// <paramref name="authenticationPolicy"/> judges.
    /// </summary>
    /// <param name="state">The state every connection shares.</param>
    /// <param name="authenticationPolicy">Judges the user name and password of every <c>CONNECT</c>.</param>
    /// <param name="saslAuthenticationPolicy">Runs the enhanced authentication of a <c>CONNECT</c> with an Authentication
    /// Method, and re-authentication; <see langword="null"/> serves no method, so every one is <c>0x8C</c>.</param>
    /// <param name="disconnectsAfterRetained">When <see langword="true"/>, the broker answers a <c>SUBSCRIBE</c> with <c>SUBACK</c> and the
    /// retained messages it releases, then sends <c>DISCONNECT</c> <c>0x00</c> and closes, discarding the client's Will
    /// (ADR-0093 decision 9); <see langword="false"/> by default.</param>
    public Mqtt5Broker(
        Mqtt5BrokerState state,
        IAuthenticationPolicy authenticationPolicy,
        ISaslAuthenticationPolicy? saslAuthenticationPolicy = null,
        bool disconnectsAfterRetained = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);

        this.state = state;
        this.authenticationPolicy = authenticationPolicy;
        this.saslAuthenticationPolicy = saslAuthenticationPolicy;
        this.disconnectsAfterRetained = disconnectsAfterRetained;
    }

    /// <summary>
    /// Gets a value indicating whether the broker ends a connection after the retained messages a <c>SUBSCRIBE</c> releases
    /// (<c>--mqtt5-disconnect-after-retained</c>, ADR-0093 decision 9).
    /// </summary>
    public bool DisconnectsAfterRetained => disconnectsAfterRetained;

    /// <summary>
    /// The schemes answered: <c>mqtt5</c>, and <c>mqtt5s</c>, the same exchange over implicit TLS,
    /// whose handshake the engine performs (ADR-0010).
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["mqtt5", "mqtt5s"]);

    /// <summary>
    /// Answers every packet on <paramref name="connection"/> until the connection ends.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        return new Mqtt5BrokerConnection(connection, context, state, authenticationPolicy, saslAuthenticationPolicy, disconnectsAfterRetained).ServeAsync();
    }
}
