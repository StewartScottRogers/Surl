namespace Surl.Extended.Protocol.Redis;

/// <summary>Answers one command whose name and argument count were already checked.</summary>
/// <param name="call">The command.</param>
/// <returns>The reply, in the connection's RESP version.</returns>
internal delegate ValueTask<RespValue> RedisCommandAnswer(RedisCommandCall call);
