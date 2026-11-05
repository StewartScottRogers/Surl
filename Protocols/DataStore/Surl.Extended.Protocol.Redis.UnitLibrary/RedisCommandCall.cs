namespace Surl.Extended.Protocol.Redis;

/// <summary>One command as a <see cref="RedisCommandAnswer"/> receives it.</summary>
/// <param name="Arguments">The command's name then its arguments, each the exact bytes sent.</param>
/// <param name="Session">The connection's state.</param>
/// <param name="Logins">Judges a login the command carries.</param>
internal sealed record RedisCommandCall(IReadOnlyList<byte[]> Arguments, RedisSession Session, RedisLogins Logins);
