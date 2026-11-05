using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The AMQP 0-9-1 broker (ADR-0112) over the shared message bus: the connection class - the
/// protocol header, <c>start</c>/<c>start-ok</c> with the login, <c>tune</c>/<c>tune-ok</c>,
/// <c>open</c>/<c>open-ok</c> of the one virtual host <c>/</c>, heartbeats, and
/// <c>close</c>/<c>close-ok</c> both ways - and channels, exchanges, queues and the basic class.
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
/// <b>Channels, exchanges and queues</b> (decisions 5 and 7). <c>channel.open</c> opens a channel
/// up to the negotiated channel-max; <c>channel.flow</c> holds and resumes its deliveries. The
/// default exchange routes to the bus queue the routing key names; <c>amq.direct</c>,
/// <c>amq.fanout</c> and declared direct, fanout and topic exchanges route inside the broker onto
/// bus queues; <c>amq.topic</c> is the bus's topic space, its bindings the bus's, so a message
/// published over another protocol reaches a queue bound there with the mapped key. Queues are the
/// bus's named queues: a declare with an empty name makes <c>amq.gen-</c> and 22 base64url
/// characters, and <c>""</c> elsewhere is the channel's last declared queue.
/// </para>
/// <para>
/// <b>The basic class</b> (decision 6). <c>basic.publish</c> is read with its content header and
/// body frames and stored with the native form <c>amqp0</c>; an unroutable <c>mandatory</c> (or
/// <c>immediate</c>) message is returned with <c>basic.return</c> 312, and a routed
/// <c>immediate</c> one no consumer took with 313. <c>basic.get</c> answers <c>get-ok</c> or
/// <c>get-empty</c>; <c>basic.consume</c> answers <c>consume-ok</c> before the first
/// <c>basic.deliver</c>. Delivery tags run from 1 per channel; <c>ack</c>, <c>reject</c>,
/// <c>nack</c> and <c>recover</c> settle them, and a tag not outstanding is 406. <c>basic.qos</c>'s
/// prefetch-count bounds each consumer the channel makes afterwards, 1000 when it is 0 or larger.
/// Topic access refusals are 403.
/// </para>
/// <para>
/// <b>Exceptions</b> (decision 7). Before <c>open-ok</c>, every fault but those two closes the
/// connection with no bytes, as does the head timeout. After it, a channel exception is
/// <c>channel.close</c> naming the method, the channel's consumers ended and its deliveries
/// requeued; the broker then discards the channel's frames until <c>close-ok</c> or <c>close</c>.
/// A connection exception is <c>connection.close</c>: a malformed frame 501, a method whose
/// arguments do not decode 502, a method only a server sends 503, a frame on a channel not open or
/// above channel-max 504, a content frame out of sequence 505, a bound reached 506, a reused
/// consumer tag or an exchange redeclared with another type 530, and a method not served
/// (<c>tx</c>, <c>confirm</c>, <c>exchange.bind</c>) 540. After a <c>close</c> it sends, the broker
/// discards every frame until <c>close-ok</c> or <c>close</c>, or for one second. The client's
/// <c>connection.close</c> is answered <c>close-ok</c>.
/// </para>
/// </remarks>
public sealed class Amqp0Broker : IConnectionProtocolServer
{
    /// <summary>The channel-max <c>connection.tune</c> offers: 256 (decision 8).</summary>
    public const ushort ChannelMax = 256;

    private readonly ISaslAuthenticationPolicy policy;
    private readonly MessageBus.MessageBus bus;
    private readonly Amqp0BrokerOptions options;
    private readonly Amqp0ExchangeTable exchanges;

    /// <summary>Creates a broker over <paramref name="bus"/> whose logins <paramref name="policy"/> judges.</summary>
    /// <param name="policy">Offers the mechanisms and judges each login.</param>
    /// <param name="bus">The process's shared message bus: its queues are the broker's, its topic space <c>amq.topic</c>.</param>
    /// <param name="options">The idle timeout and whether <c>ANONYMOUS</c> is offered; <see cref="Amqp0BrokerOptions.Default"/> when omitted.</param>
    public Amqp0Broker(ISaslAuthenticationPolicy policy, MessageBus.MessageBus bus, Amqp0BrokerOptions? options = null)
        : this(policy, bus, options, new Amqp0ExchangeTable())
    {
    }

    // The tests' seam: an exchange table with smaller bounds.
    internal Amqp0Broker(ISaslAuthenticationPolicy policy, MessageBus.MessageBus bus, Amqp0BrokerOptions? options, Amqp0ExchangeTable exchanges)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(bus);

        this.policy = policy;
        this.bus = bus;
        this.options = options ?? Amqp0BrokerOptions.Default;
        this.exchanges = exchanges;
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

        return new Amqp0BrokerConnection(connection, context, policy, options, bus, exchanges).ServeAsync();
    }
}
