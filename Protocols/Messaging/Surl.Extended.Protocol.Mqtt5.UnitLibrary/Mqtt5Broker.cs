using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// The MQTT 5.0 broker (ADR-0093): answers <c>CONNECT</c> with <c>CONNACK</c>, judges the login,
/// holds the client to its Keep Alive, and publishes its Will when the connection ends without a
/// normal <c>DISCONNECT</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Before CONNACK</b> (ADR-0093 decisions 1 and 6). The first packet must be <c>CONNECT</c>; any
/// other, bytes that are not a readable fixed header, a protocol name that is not MQTT, and a first
/// packet not complete within <see cref="ExchangeLimits.HeadTimeout"/> close the connection with no
/// bytes. A <c>CONNECT</c> that is refused is answered <c>CONNACK</c> with Session Present 0 and its
/// reason code, then closed: <c>0x84</c> for <c>MQTT</c> or <c>MQIsdp</c> at a level other than 5,
/// <c>0x81</c> for a malformed one, <c>0x82</c> for a property twice or a value MQTT 5.0 forbids,
/// <c>0x95</c> for one over <see cref="ExchangeLimits.MaxMessageBytes"/>, and <c>0x8C</c> for an
/// Authentication Method (no method is offered yet). The user name, the password and the
/// connection's <see cref="IConnection.TlsSession"/> then go to the
/// <see cref="IAuthenticationPolicy"/>: accepted is <c>0x00</c>; refused credentials <c>0x86</c>;
/// no user name, or a password over a connection with no TLS session, <c>0x87</c> (ADR-0093
/// decision 2).
/// </para>
/// <para>
/// <b>The CONNACK of an accepted CONNECT</b> carries Session Present 0 and, in this order: Session
/// Expiry Interval <see cref="MaxSessionExpirySeconds"/> when the client asked for more; Receive
/// Maximum <see cref="ReceiveMaximum"/>; Maximum Packet Size
/// <see cref="ExchangeLimits.MaxMessageBytes"/> unless it is 0; the Assigned Client Identifier
/// when the client sent a zero-length one; Topic Alias Maximum <see cref="TopicAliasMaximum"/>;
/// and Server Keep Alive <see cref="MaxKeepAliveSeconds"/> when the client's Keep Alive was 0 or
/// above it.
/// </para>
/// <para>
/// <b>After CONNACK.</b> <c>PINGREQ</c> is answered <c>PINGRESP</c>. A client <c>DISCONNECT</c>
/// closes the connection: <c>0x00</c> discards the Will, any other reason code keeps it. Silence
/// for one and a half times the Keep Alive in force is answered <c>DISCONNECT</c> <c>0x8D</c>. A
/// second <c>CONNECT</c>, a packet only a server sends, and <c>AUTH</c> (no method was used) are
/// answered <c>DISCONNECT</c> <c>0x82</c>; a refused packet <c>DISCONNECT</c> with its fault's
/// reason code. <c>PUBLISH</c> and its acknowledgements, <c>SUBSCRIBE</c> and <c>UNSUBSCRIBE</c>
/// are answered <c>DISCONNECT</c> <c>0x83</c> Implementation specific error until BL-497 and
/// BL-498 answer them. Each of those <c>DISCONNECT</c>s closes the connection.
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

    /// <summary>
    /// Creates a broker whose connections share <paramref name="state"/> and whose logins
    /// <paramref name="authenticationPolicy"/> judges.
    /// </summary>
    /// <param name="state">The state every connection shares.</param>
    /// <param name="authenticationPolicy">Judges the user name and password of every <c>CONNECT</c>.</param>
    public Mqtt5Broker(Mqtt5BrokerState state, IAuthenticationPolicy authenticationPolicy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);

        this.state = state;
        this.authenticationPolicy = authenticationPolicy;
    }

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

        return new Mqtt5BrokerConnection(connection, context, state, authenticationPolicy).ServeAsync();
    }
}
