namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Turns the time a command names into an absolute expiry in Unix milliseconds, refusing one that
/// overflows a signed 64-bit integer as Redis does (the <c>SET</c> and <c>EXPIRE</c> pages).
/// </summary>
internal static class RedisExpiryTimes
{
    /// <summary>
    /// <paramref name="amount"/> seconds or milliseconds after <paramref name="baseMilliseconds"/>
    /// (the time now for a relative option, 0 for an absolute one).
    /// </summary>
    /// <param name="amount">The time the command named.</param>
    /// <param name="isSeconds">Whether it is in seconds rather than milliseconds.</param>
    /// <param name="baseMilliseconds">What it is counted from, in Unix milliseconds.</param>
    /// <param name="expiresAtMilliseconds">The expiry, when it fits.</param>
    /// <returns>Whether it fits in a signed 64-bit integer.</returns>
    public static bool TryAbsolute(long amount, bool isSeconds, long baseMilliseconds, out long expiresAtMilliseconds)
    {
        expiresAtMilliseconds = 0;
        if (isSeconds && (amount > long.MaxValue / 1000 || amount < long.MinValue / 1000))
        {
            return false;
        }

        var milliseconds = isSeconds ? amount * 1000 : amount;
        if (milliseconds > long.MaxValue - baseMilliseconds)
        {
            return false;
        }

        expiresAtMilliseconds = milliseconds + baseMilliseconds;
        return true;
    }
}
