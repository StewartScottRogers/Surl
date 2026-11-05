using System.Security.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// The NATS server for the <c>nats</c> scheme the Curl port adds (ADR-0083; ADR-0113), judged by
/// the NATS client protocol page, never by the Curl port.
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
/// Each of these is answered <c>-ERR</c> and the connection closed (decision 7): an operation
/// before CONNECT, refused credentials, or an NKey or JWT login (<c>'Authorization Violation'</c>);
/// no CONNECT within the head timeout (<c>'Authorization Timeout'</c>); a line over
/// <c>--max-line</c> (<c>'Maximum Control Line Exceeded'</c>); CONNECT that is not a JSON object
/// of the right field types, or PING or PONG with arguments (<c>'Parser Error'</c>); a
/// <c>protocol</c> other than 0 or 1 (<c>'Invalid Client Protocol'</c>); <c>no_responders</c>
/// without <c>headers</c> (<c>'No Responders Requires Headers Support'</c>); and any other
/// operation, a second CONNECT included (<c>'Unknown Protocol Operation'</c>). Publishing and
/// subscribing come with the message bus (BL-526), and <c>natss</c> with BL-527.
/// </para>
/// </remarks>
public sealed class NatsServer : IConnectionProtocolServer, IConnectionRefusalWriter
{
    /// <summary>The length of <see cref="ServerId"/>.</summary>
    public const int ServerIdLength = 22;

    private const string ServerIdAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private readonly IAuthenticationPolicy policy;
    private readonly NatsServerOptions options;
    private long lastClientId;

    /// <summary>Creates the server with a random <see cref="ServerId"/>.</summary>
    /// <param name="policy">Judges each CONNECT's login.</param>
    /// <param name="options">The idle timeout and <c>--allow-anonymous</c>; <see cref="NatsServerOptions.Default"/> when null.</param>
    public NatsServer(IAuthenticationPolicy policy, NatsServerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(policy);

        this.policy = policy;
        this.options = options ?? NatsServerOptions.Default;
        ServerId = RandomNumberGenerator.GetString(ServerIdAlphabet, ServerIdLength);
    }

    /// <summary>
    /// The server's identity, 22 upper-case letters and digits drawn once when it is created, sent
    /// in INFO as <c>server_id</c> and <c>server_name</c>.
    /// </summary>
    public string ServerId { get; }

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["nats"]);

    /// <inheritdoc/>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var info = new NatsInfo(
            ServerId,
            connection.LocalEndPoint,
            connection.RemoteEndPoint,
            (ulong)Interlocked.Increment(ref lastClientId),
            context.Limits.MaxMessageBytes,
            !options.AllowsAnonymous);
        return new NatsServerConnection(connection, context, policy, options, info).ServeAsync();
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
