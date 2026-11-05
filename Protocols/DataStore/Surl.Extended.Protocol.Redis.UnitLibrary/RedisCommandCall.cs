namespace Surl.Extended.Protocol.Redis;

/// <summary>One command as a <see cref="RedisCommandAnswer"/> receives it.</summary>
/// <param name="Arguments">The command's name then its arguments, each the exact bytes sent.</param>
/// <param name="Session">The connection's state.</param>
/// <param name="Logins">Judges a login the command carries.</param>
/// <param name="Keyspace">The server's 16 databases, shared by every connection.</param>
/// <param name="NowMilliseconds">The injected clock's time when the command was read, in Unix milliseconds.</param>
internal sealed record RedisCommandCall(
    IReadOnlyList<byte[]> Arguments,
    RedisSession Session,
    RedisLogins Logins,
    RedisKeyspace Keyspace,
    long NowMilliseconds)
{
    /// <summary>The database the connection selected.</summary>
    public RedisDatabase Database => Keyspace[Session.DatabaseIndex];

    /// <summary>A missing value in the connection's version: <c>$-1</c> in RESP2, <c>_</c> in RESP3.</summary>
    public RespValue NullReply => Session.NullReply;
}
