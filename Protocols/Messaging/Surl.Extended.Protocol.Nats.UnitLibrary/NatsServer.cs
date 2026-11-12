using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// The NATS server for the <c>nats</c> and <c>natss</c> schemes the Curl port adds (ADR-0083; ADR-0113), judged by
/// the NATS client protocol page, never by the Curl port. Its subscriptions and routing are the
/// shared <see cref="Surl.MessageBus.MessageBus"/>'s, so a NATS subscriber receives what every
/// other messaging server publishes, and the reverse.
/// </summary>
/// <remarks>
/// <para>
/// Each connection gets INFO first (ADR-0113 decision 1), then must send CONNECT within the head
/// timeout: CONNECT's JSON is read with <c>Utf8JsonReader</c>, its login judged by the
/// <see cref="IAuthenticationPolicy"/> (decisions 2 and 3), and <c>+OK</c> sent under
/// <c>verbose</c>. Operation names match case-insensitively. A client's PING is answered PONG; the
/// server sends its own PING every <see cref="NatsServerOptions.PingInterval"/> and closes with
/// <c>-ERR 'Stale Connection'</c> once <see cref="NatsServerOptions.MaxUnansweredPings"/> go
/// unanswered (decision 5).
/// </para>
/// <para>
/// After CONNECT (decision 4), PUB and HPUB publish on the bus for the logged-in account, SUB
/// subscribes with an optional queue group (one member per message), and UNSUB ends a subscription
/// at once or after <c>max_msgs</c> deliveries in all. Deliveries go out as MSG, or as HMSG with
/// the publisher's header block byte for byte when CONNECT set <c>headers</c>; under
/// <c>echo: false</c> a connection's own publishes skip its own subscriptions; and a request no
/// subscription took is answered with the <c>503</c> status message under <c>no_responders</c>.
/// Answered <c>-ERR</c> with the connection kept: an invalid SUB subject (<c>'Invalid
/// Subject'</c>), an invalid publish subject or reply-to (<c>'Invalid Publish Subject'</c>), a
/// publish or subscription <c>--topic-access</c> refuses (<c>'Permissions Violation for Publish
/// to &lt;subject&gt;'</c>, <c>'... Subscription to &lt;subject&gt;'</c>), and a SUB past the bus's
/// subscriptions per connection (<c>'Maximum Subscriptions Exceeded'</c>).
/// </para>
/// <para>
/// Each of these is answered <c>-ERR</c> and the connection closed (decision 7): an operation
/// before CONNECT, refused credentials, or an NKey or JWT login (<c>'Authorization Violation'</c>);
/// no CONNECT within the head timeout (<c>'Authorization Timeout'</c>); a line over
/// <c>--max-line</c> (<c>'Maximum Control Line Exceeded'</c>); CONNECT that is not a JSON object
/// of the right field types, PING or PONG with arguments, PUB, HPUB, SUB or UNSUB with malformed
/// arguments, or a payload not followed by CR LF (<c>'Parser Error'</c>); a payload over
/// <c>max_payload</c> (<c>'Maximum Payload Violation'</c>); a full bus store (<c>'Resource Limit
/// Exceeded'</c>); a <c>protocol</c> other than 0 or 1 (<c>'Invalid Client Protocol'</c>);
/// <c>no_responders</c> without <c>headers</c> (<c>'No Responders Requires Headers Support'</c>);
/// more than <see cref="NatsServerOptions.MaxPendingBytes"/> waiting unwritten (<c>'Slow
/// Consumer'</c>, best effort); and any other operation, HPUB without <c>headers</c> and a second
/// CONNECT included (<c>'Unknown Protocol Operation'</c>).
/// </para>
/// <para>
/// <c>nats</c> never offers TLS. On <c>natss</c> (decision 6) TLS comes from the listener seam
/// before CONNECT, in either form: a client that speaks within
/// <see cref="NatsServerOptions.TlsFirstWait"/> of accept gets the handshake at once and INFO inside
/// TLS; otherwise INFO says <c>tls_required: true</c> in plain text, and the client's next byte must
/// begin a TLS handshake record (<c>0x16</c>), or it is answered <c>-ERR 'Secure Connection - TLS
/// Required'</c> and closed. No handshake begun within the head timeout is <c>-ERR 'Authorization
/// Timeout'</c>; a handshake that fails, or does not finish within it, closes with no bytes.
/// </para>
/// </remarks>
public sealed class NatsServer : IConnectionProtocolServer, IConnectionRefusalWriter
{
    /// <summary>The length of <see cref="ServerId"/>.</summary>
    public const int ServerIdLength = 22;

    /// <summary>The scheme whose connections must run TLS before CONNECT (ADR-0113 decision 6).</summary>
    public const string TlsScheme = "natss";

    private const string ServerIdAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private readonly IAuthenticationPolicy policy;
    private readonly Surl.MessageBus.MessageBus bus;
    private readonly NatsServerOptions options;
    private long lastClientId;

    /// <summary>Creates the server with a <see cref="ServerId"/> drawn from <paramref name="identifiers"/>.</summary>
    /// <param name="policy">Judges each CONNECT's login.</param>
    /// <param name="bus">The message bus every messaging server of the process shares.</param>
    /// <param name="options">The idle timeout and <c>--allow-anonymous</c>; <see cref="NatsServerOptions.Default"/> when null.</param>
    /// <param name="identifiers">Where the server ID's bytes come from (ADR-0124, decision 6); a source of random bytes that differ every run when null.</param>
    public NatsServer(
        IAuthenticationPolicy policy,
        Surl.MessageBus.MessageBus bus,
        NatsServerOptions? options = null,
        IIdentifierRandom? identifiers = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(bus);

        this.policy = policy;
        this.bus = bus;
        this.options = options ?? NatsServerOptions.Default;
        ServerId = DrawServerId(identifiers ?? NatsUnseededIdentifierRandom.Instance);
    }

    /// <summary>
    /// Draws <see cref="ServerIdLength"/> characters of the alphabet, one byte each; a byte at or above the
    /// largest multiple of the alphabet's length is drawn again, so every character is equally likely.
    /// </summary>
    internal static string DrawServerId(IIdentifierRandom identifiers)
    {
        var limit = 256 - (256 % ServerIdAlphabet.Length);
        Span<byte> one = stackalloc byte[1];
        var characters = new char[ServerIdLength];
        for (var index = 0; index < characters.Length; index++)
        {
            do
            {
                identifiers.NextBytes(one);
            }
            while (one[0] >= limit);

            characters[index] = ServerIdAlphabet[one[0] % ServerIdAlphabet.Length];
        }

        return new string(characters);
    }

    /// <summary>
    /// The server's identity, 22 upper-case letters and digits drawn once when it is created, sent
    /// in INFO as <c>server_id</c> and <c>server_name</c>.
    /// </summary>
    public string ServerId { get; }

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["nats", TlsScheme]);

    /// <inheritdoc/>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        // ADR-0113 decision 7: the smaller of --max-message and the bus's bound, and never more
        // than one array holds.
        var maxPayload = Math.Min(Math.Min(context.Limits.MaxMessageBytes, bus.MaxMessageBytes), Array.MaxLength - 2);
        var info = new NatsInfo(
            ServerId,
            connection.LocalEndPoint,
            connection.RemoteEndPoint,
            (ulong)Interlocked.Increment(ref lastClientId),
            maxPayload,
            !options.AllowsAnonymous,
            string.Equals(context.Scheme, TlsScheme, StringComparison.OrdinalIgnoreCase));
        using var serving = new NatsServerConnection(connection, context, policy, bus, options, info);
        await serving.ServeAsync();
    }

    /// <summary>
    /// Answers a connection past <c>--max-connections</c> or <c>--max-connections-per-address</c>
    /// with <c>-ERR 'Maximum Connections Exceeded'</c> and no INFO, then closes it (ADR-0113
    /// decision 7).
    /// </summary>
    /// <param name="connection">The refused connection.</param>
    /// <param name="refusal">Which limit refused it; both are answered alike.</param>
    /// <param name="cancellationToken">Ends the write.</param>
    /// <returns>A task that completes when the refusal is written.</returns>
    public async ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await connection.WriteAsync(NatsErrors.Line(NatsErrors.MaximumConnectionsExceeded), cancellationToken);
        await connection.CompleteWritesAsync(cancellationToken);
    }
}
