namespace Surl.Extended.Protocol.Redis;

/// <summary>One of <c>SET</c>'s expiry options: <c>EX</c>, <c>PX</c>, <c>EXAT</c> or <c>PXAT</c> (the <c>SET</c> page).</summary>
/// <param name="IsSeconds">Whether its time is in seconds rather than milliseconds.</param>
/// <param name="IsRelative">Whether its time counts from now rather than from the Unix epoch.</param>
internal sealed record RedisSetExpiry(bool IsSeconds, bool IsRelative)
{
    private static readonly RedisSetExpiry Ex = new(true, true);
    private static readonly RedisSetExpiry Px = new(false, true);
    private static readonly RedisSetExpiry ExAt = new(true, false);
    private static readonly RedisSetExpiry PxAt = new(false, false);

    /// <summary>The option named <paramref name="upperCaseName"/>, or <see langword="null"/> when it names none.</summary>
    /// <param name="upperCaseName">The option's name in upper case.</param>
    /// <returns>The option.</returns>
    public static RedisSetExpiry? Named(string upperCaseName) => upperCaseName switch
    {
        "EX" => Ex,
        "PX" => Px,
        "EXAT" => ExAt,
        "PXAT" => PxAt,
        _ => null,
    };
}
