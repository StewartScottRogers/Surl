using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Redis.RedisTestExchanges;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// One <see cref="RedisServer"/> letting anonymous callers in, on a manual clock, whose
/// connections share its keyspace: a test sends commands on one connection, moves the clock, and
/// sends more on the next.
/// </summary>
internal sealed class RedisTestServer
{
    private readonly RedisServer server;

    public RedisTestServer(RedisKeyspace? keyspace = null, RedisPubSub? pubSub = null, MessageBus.MessageBus? bus = null)
    {
        Keyspace = keyspace ?? new RedisKeyspace();
        Bus = bus ?? new MessageBus.MessageBus(Clock);
        server = new RedisServer(new UnitTestRedisAuthenticationPolicy(true), Keyspace, pubSub ?? new RedisPubSub(Bus));
    }

    public ManualTimeProvider Clock { get; } = new();

    /// <summary>The message bus the server's pub/sub runs on, which a test publishes and subscribes on as another protocol would.</summary>
    public MessageBus.MessageBus Bus { get; }

    public RedisKeyspace Keyspace { get; }

    /// <summary>The clock's time in Unix milliseconds.</summary>
    public long NowMilliseconds => Clock.GetUtcNow().ToUnixTimeMilliseconds();

    /// <summary>Sends <paramref name="commands"/> on a new connection and returns what the server wrote.</summary>
    public async Task<string> SendAsync(CancellationToken cancellationToken, params string[] commands)
    {
        var connection = Connection([string.Concat(commands)]);
        await server.ServeAsync(connection, Context(cancellationToken, Clock));
        return Written(connection);
    }

    /// <summary>
    /// Sends <paramref name="commands"/> on a new connection the client keeps open, as a
    /// subscriber does, and returns it once they are answered.
    /// </summary>
    public async Task<OpenRedisConnection> OpenAsync(CancellationToken cancellationToken, params string[] commands)
    {
        var connection = Connection([string.Concat(commands)], peerHalfCloses: false);
        var log = new RecordingExchangeLog();
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var serving = server.ServeAsync(connection, Context(lifetime.Token, Clock, log: log));
        await Task.Yield();
        return new OpenRedisConnection(connection, serving, lifetime, log);
    }
}
