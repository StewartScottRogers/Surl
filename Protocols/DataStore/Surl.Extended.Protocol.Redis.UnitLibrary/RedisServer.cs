using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The Redis server (ADR-0106): reads each connection's commands in order and answers each in
/// turn, in RESP2 until <c>HELLO 3</c> switches the connection to RESP3.
/// </summary>
/// <remarks>
/// <para>
/// <b>Commands served so far</b> (ADR-0106 decision 1): the connection commands (BL-489)
/// <c>HELLO</c>, <c>AUTH</c>, <c>SELECT</c>, <c>PING</c>, <c>ECHO</c>, <c>QUIT</c>,
/// <c>RESET</c> and <c>CLIENT SETNAME</c>, <c>GETNAME</c>, <c>ID</c> and <c>SETINFO</c>; the key
/// commands (BL-490) <c>DEL</c>, <c>UNLINK</c>, <c>EXISTS</c>, <c>EXPIRE</c>, <c>PEXPIRE</c>,
/// <c>TTL</c>, <c>PTTL</c>, <c>PERSIST</c>, <c>TYPE</c>, <c>RENAME</c>, <c>KEYS</c>, <c>SCAN</c>,
/// <c>DBSIZE</c>, <c>FLUSHDB</c> and <c>FLUSHALL</c>; and the string commands (BL-490) <c>GET</c>,
/// <c>SET</c>, <c>GETDEL</c>, <c>MGET</c>, <c>MSET</c>, <c>INCR</c>, <c>DECR</c>, <c>INCRBY</c>,
/// <c>DECRBY</c>, <c>APPEND</c> and <c>STRLEN</c>; and (BL-491) the hash commands <c>HSET</c>,
/// <c>HGET</c>, <c>HMGET</c>, <c>HDEL</c>, <c>HEXISTS</c>, <c>HLEN</c>, <c>HKEYS</c>, <c>HVALS</c>,
/// <c>HGETALL</c>, <c>HINCRBY</c> and <c>HSETNX</c>, the list commands <c>LPUSH</c>, <c>RPUSH</c>,
/// <c>LPOP</c>, <c>RPOP</c>, <c>LLEN</c>, <c>LRANGE</c>, <c>LINDEX</c>, <c>LSET</c>, <c>LREM</c> and
/// <c>LTRIM</c>, the set commands <c>SADD</c>, <c>SREM</c>, <c>SMEMBERS</c>, <c>SISMEMBER</c>,
/// <c>SMISMEMBER</c>, <c>SCARD</c>, <c>SPOP</c>, <c>SINTER</c>, <c>SUNION</c> and <c>SDIFF</c>, and
/// pub/sub: <c>SUBSCRIBE</c>, <c>UNSUBSCRIBE</c>, <c>PSUBSCRIBE</c>, <c>PUNSUBSCRIBE</c> and
/// <c>PUBLISH</c>, on the shared message bus for a channel that is a bus topic and among this
/// server's connections for any other (ADR-0100 decision 3.8, BL-678). Any other command is
/// <c>-ERR unknown command '…', with args beginning with: …</c>, the connection kept.
/// </para>
/// <para>
/// <b>Keyspace</b> (ADR-0106 decision 3). Each server holds 16 databases in memory, shared by
/// all its connections; nothing is written to disk, so a new server starts empty. Expiry runs on
/// the exchange's injected clock and a key is absent from the instant its expiry comes.
/// </para>
/// <para>
/// <b>Login</b> (ADR-0106 decision 4). Each connection asks the policy once whether an anonymous
/// caller is let in (<c>--allow-anonymous</c>). When not, every command but <c>AUTH</c>,
/// <c>HELLO</c>, <c>QUIT</c> and <c>RESET</c> is <c>-NOAUTH Authentication required.</c> until a
/// login is accepted, and commands are read under Redis 7's unauthenticated limits.
/// </para>
/// <para>
/// <b>Limits</b> (ADR-0106 decision 6). A framing error is answered with its
/// <c>-ERR Protocol error: …</c> and the connection closed; a command too large closes it with no
/// bytes; so does no complete first command within <see cref="ExchangeLimits.HeadTimeout"/> of
/// accept, or no complete later command within it of that command's first byte. A connection
/// past the connection limits is answered <c>-ERR max number of clients reached</c>. A connection
/// subscribes to at most 10000 channels and patterns, and one with more than 32 MiB of messages
/// unsent is closed with no more bytes.
/// </para>
/// </remarks>
public sealed class RedisServer : IConnectionProtocolServer, IConnectionRefusalWriter
{
    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly RedisKeyspace keyspace;
    private readonly RedisPubSub pubSub;
    private long lastClientId;

    /// <summary>
    /// Creates a server whose logins <paramref name="authenticationPolicy"/> judges, on a message
    /// bus of its own, so its pub/sub reaches only its own connections.
    /// </summary>
    /// <param name="authenticationPolicy">
    /// Judges <c>AUTH</c> and <c>HELLO … AUTH</c>, and is asked once a connection whether an
    /// anonymous caller is let in.
    /// </param>
    public RedisServer(IAuthenticationPolicy authenticationPolicy)
        : this(authenticationPolicy, new MessageBus.MessageBus(TimeProvider.System))
    {
    }

    /// <summary>
    /// Creates a server whose logins <paramref name="authenticationPolicy"/> judges and whose
    /// pub/sub channels that are bus topics are published and subscribed on
    /// <paramref name="messageBus"/>, the bus the process's messaging servers share (ADR-0100
    /// decision 3.8).
    /// </summary>
    /// <param name="authenticationPolicy">
    /// Judges <c>AUTH</c> and <c>HELLO … AUTH</c>, and is asked once a connection whether an
    /// anonymous caller is let in.
    /// </param>
    /// <param name="messageBus">The message bus the process's messaging servers share.</param>
    public RedisServer(IAuthenticationPolicy authenticationPolicy, MessageBus.MessageBus messageBus)
        : this(authenticationPolicy, new RedisKeyspace(), new RedisPubSub(messageBus ?? throw new ArgumentNullException(nameof(messageBus))))
    {
    }

    /// <summary>Creates a server holding its keys in <paramref name="keyspace"/> and its channels in <paramref name="pubSub"/>, for tests of their bounds.</summary>
    /// <param name="authenticationPolicy">Judges logins.</param>
    /// <param name="keyspace">The 16 databases every connection shares.</param>
    /// <param name="pubSub">The channels and patterns every connection shares.</param>
    internal RedisServer(IAuthenticationPolicy authenticationPolicy, RedisKeyspace keyspace, RedisPubSub pubSub)
    {
        ArgumentNullException.ThrowIfNull(authenticationPolicy);

        this.authenticationPolicy = authenticationPolicy;
        this.keyspace = keyspace;
        this.pubSub = pubSub;
    }

    /// <summary>
    /// The schemes answered: <c>redis</c>, and <c>rediss</c>, the same exchange over implicit TLS,
    /// whose handshake the engine performs (ADR-0106 decision 7).
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["redis", "rediss"]);

    /// <summary>
    /// Answers every command on <paramref name="connection"/> until the connection ends. Each
    /// connection gets the next client id, from 1, in the order connections are served.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var clientId = Interlocked.Increment(ref lastClientId);
        return new RedisServerConnection(connection, context, authenticationPolicy, clientId, keyspace, pubSub).ServeAsync();
    }

    /// <summary>
    /// Answers a connection past <c>--max-connections</c> or <c>--max-connections-per-address</c>
    /// with <c>-ERR max number of clients reached</c>, then closes it (ADR-0106 decision 6).
    /// </summary>
    /// <param name="connection">The refused connection.</param>
    /// <param name="refusal">Which limit refused it; both are answered alike.</param>
    /// <param name="cancellationToken">Ends the write.</param>
    /// <returns>A task that completes when the refusal is written.</returns>
    public async ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await connection.WriteAsync(RedisErrorReplies.MaxNumberOfClientsReached.Encode(), cancellationToken);
        await connection.CompleteWritesAsync(cancellationToken);
    }
}
