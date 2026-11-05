using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The AMQP 0-9-1 broker (ADR-0112): so far, the connection class - the protocol header,
/// <c>start</c>/<c>start-ok</c> with the login, <c>tune</c>/<c>tune-ok</c>, <c>open</c>/<c>open-ok</c>
/// of the one virtual host <c>/</c>, heartbeats, and <c>close</c>/<c>close-ok</c> both ways.
/// </summary>
/// <remarks>
/// <para>
/// <b>The login</b> (decision 2). <c>connection.start</c> offers the policy's mechanisms for the
/// scheme and TLS state, <c>AMQPLAIN</c> right after <c>PLAIN</c>, then <c>ANONYMOUS</c> when
/// <see cref="Amqp0BrokerOptions.OffersAnonymous"/> is set. A mechanism not offered closes the
/// connection with no bytes. <c>AMQPLAIN</c>'s <c>LOGIN</c> and <c>PASSWORD</c> go to the policy as a
/// <c>PLAIN</c> response; <c>ANONYMOUS</c> is accepted at once; every other mechanism runs through
/// the policy's exchange, a challenge as <c>connection.secure</c> and an answer as
/// <c>secure-ok</c>. A refused login is answered <c>connection.close</c> 403.
/// </para>
/// <para>
/// <b>Tuning</b> (decision 3). <c>connection.tune</c> offers channel-max
/// <see cref="ChannelMax"/>, the frame-max <see cref="Amqp0FrameLimits.OfferedFrameMax"/> gives
/// for <see cref="ExchangeLimits.MaxMessageBytes"/>, and
/// <see cref="Amqp0BrokerOptions.OfferedHeartbeatSeconds"/>. A <c>tune-ok</c> above either maximum,
/// or with a nonzero frame-max below 4096, closes the connection with no bytes. With a nonzero
/// heartbeat <c>h</c>, the broker sends a heartbeat frame whenever it has written nothing for
/// <c>h</c> seconds, and closes the connection without <c>connection.close</c> when no frame has
/// arrived for <c>2h</c>.
/// </para>
/// <para>
/// <b>The virtual host</b> (decision 4). <c>connection.open</c> of <c>/</c> is answered
/// <c>open-ok</c>; any other name <c>connection.close</c> 530.
/// </para>
/// <para>
/// <b>Exceptions</b> (decision 7). Before <c>open-ok</c>, every fault but those two closes the
/// connection with no bytes, as does the head timeout. After it, a malformed frame is
/// <c>connection.close</c> 501, a method whose arguments do not decode 502, a method on channel 0
/// that is not <c>connection.close</c> 503, a frame on any other channel 504 (no channel is open
/// yet: channels are BL-506's), and a content frame on channel 0 505. After a <c>close</c> it
/// sends, the broker discards every frame until <c>close-ok</c> or <c>close</c>, or for one second.
/// The client's <c>connection.close</c> is answered <c>close-ok</c>.
/// </para>
/// </remarks>
public sealed class Amqp0Broker : IConnectionProtocolServer
{
    /// <summary>The channel-max <c>connection.tune</c> offers: 256 (decision 8).</summary>
    public const ushort ChannelMax = 256;

    private readonly ISaslAuthenticationPolicy policy;
    private readonly Amqp0BrokerOptions options;

    /// <summary>Creates a broker whose logins <paramref name="policy"/> judges.</summary>
    /// <param name="policy">Offers the mechanisms and judges each login.</param>
    /// <param name="options">The idle timeout and whether <c>ANONYMOUS</c> is offered; <see cref="Amqp0BrokerOptions.Default"/> when omitted.</param>
    public Amqp0Broker(ISaslAuthenticationPolicy policy, Amqp0BrokerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(policy);

        this.policy = policy;
        this.options = options ?? Amqp0BrokerOptions.Default;
    }

    /// <summary>
    /// The schemes answered: <c>amqp</c>, and <c>amqps</c>, the same exchange over implicit TLS,
    /// whose handshake the engine performs (decision 8).
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["amqp", "amqps"]);

    /// <summary>Answers <paramref name="connection"/> until it ends.</summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        return new Amqp0BrokerConnection(connection, context, policy, options).ServeAsync();
    }
}
