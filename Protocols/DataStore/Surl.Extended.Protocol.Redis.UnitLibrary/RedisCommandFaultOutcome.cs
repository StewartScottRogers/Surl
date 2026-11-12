namespace Surl.Extended.Protocol.Redis;

/// <summary>What a fault rule makes the server do with a command about to run.</summary>
/// <param name="IsDropped">Whether the connection was aborted in place of an answer.</param>
/// <param name="Error">The chosen error to answer in place of running the command, or <see langword="null"/>.</param>
internal readonly record struct RedisCommandFaultOutcome(bool IsDropped, RespSimpleError? Error)
{
    /// <summary>No rule applies, or a delay was held: the command runs as usual.</summary>
    public static RedisCommandFaultOutcome None => default;

    /// <summary>The connection was aborted: nothing more is sent.</summary>
    public static RedisCommandFaultOutcome Dropped => new(true, null);
}
