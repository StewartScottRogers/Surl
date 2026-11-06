using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// The STOMP broker for the <c>stomp</c> scheme the Curl port adds (ADR-0083; ADR-0118), judged by
/// the STOMP 1.2 specification, never by the Curl port.
/// </summary>
/// <remarks>
/// <para>
/// Each connection must send CONNECT or STOMP within the head timeout, or is closed with no bytes.
/// The version is the highest of 1.0, 1.1 and 1.2 the client's <c>accept-version</c> names (1.0
/// without one); the login (<c>login</c> and <c>passcode</c>) is judged by the
/// <see cref="IAuthenticationPolicy"/>; and CONNECTED carries <c>version</c>, <c>heart-beat</c>
/// (not in 1.0), a random 32-hex-digit <c>session</c> and <c>server:surl</c> (decisions 1 to 3).
/// DISCONNECT ends the connection, after a RECEIPT when it carries <c>receipt</c>.
/// </para>
/// <para>
/// Messages route through the shared <see cref="Surl.MessageBus.MessageBus"/> (decisions 4 to 7):
/// <c>/topic/&lt;t&gt;</c> is bus topic <c>&lt;t&gt;</c> with live fan-out, <c>/queue/&lt;q&gt;</c>
/// the bus queue <c>&lt;q&gt;</c>, created durable on first use, with competing consumers. SEND,
/// SUBSCRIBE, UNSUBSCRIBE, ACK and NACK (cumulative under <c>client</c>, single under
/// <c>client-individual</c>), BEGIN, COMMIT and ABORT are served, a <c>receipt</c> is answered
/// RECEIPT once its frame took effect, unacknowledged queue messages go back to their queue when
/// their subscription ends, and heart-beats are sent and checked as negotiated.
/// </para>
/// <para>
/// Each of these is answered ERROR and the connection closed after lingering up to a second
/// (decisions 6 and 8): no common version (the specification's version ERROR), refused
/// credentials, any frame before CONNECT, a second CONNECT, an unknown command, a malformed frame,
/// an invalid <c>heart-beat</c>, a line over <c>--max-line</c>, more than 128 headers, a body
/// over <c>--max-message</c>, a missing header, an unknown destination, a refusal by topic access,
/// a duplicate subscription or transaction, an unknown transaction, an invalid ack mode, too many
/// subscriptions, and a full bus or a slow consumer (<c>Resource limit exceeded</c>);
/// <c>stomps</c> comes with BL-533.
/// </para>
/// </remarks>
/// <param name="policy">Judges each CONNECT's login.</param>
/// <param name="bus">The message bus every messaging server of the process shares.</param>
/// <param name="options">The idle timeout; <see cref="StompServerOptions.Default"/> when null.</param>
public sealed class StompServer(IAuthenticationPolicy policy, Surl.MessageBus.MessageBus bus, StompServerOptions? options = null)
    : IConnectionProtocolServer, IConnectionRefusalWriter
{
    private readonly IAuthenticationPolicy policy = policy ?? throw new ArgumentNullException(nameof(policy));
    private readonly Surl.MessageBus.MessageBus bus = bus ?? throw new ArgumentNullException(nameof(bus));
    private readonly StompServerOptions options = options ?? StompServerOptions.Default;

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["stomp"]);

    /// <inheritdoc/>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        return new StompServerConnection(connection, context, policy, bus, options).ServeAsync();
    }

    /// <summary>
    /// Answers a connection past <c>--max-connections</c> or <c>--max-connections-per-address</c>
    /// with ERROR <c>message:Too many connections</c> before CONNECT is read, then closes it
    /// (ADR-0118 decision 8).
    /// </summary>
    /// <param name="connection">The refused connection.</param>
    /// <param name="refusal">Which limit refused it; both are answered alike.</param>
    /// <param name="cancellationToken">Ends the write.</param>
    /// <returns>A task that completes when the refusal is written.</returns>
    public async ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await connection.WriteAsync(StompFrameWriter.Encode(StompErrors.Frame(StompErrors.TooManyConnections), null), cancellationToken);
        await connection.CompleteWritesAsync(cancellationToken);
    }
}
