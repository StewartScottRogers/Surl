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

    public RedisTestServer(RedisKeyspace? keyspace = null)
    {
        Keyspace = keyspace ?? new RedisKeyspace();
        server = new RedisServer(new UnitTestRedisAuthenticationPolicy(true), Keyspace);
    }

    public ManualTimeProvider Clock { get; } = new();

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
}
