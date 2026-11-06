namespace Surl.Extended.Protocol.Redis;

/// <summary>Reads and applies a <see cref="RedisExpireCondition"/>.</summary>
internal static class RedisExpireConditions
{
    /// <summary>Reads the options from <c>EXPIRE key time</c>'s fourth argument on.</summary>
    /// <param name="arguments">The command's name then its arguments.</param>
    /// <param name="condition">The condition, when the options read.</param>
    /// <returns>Whether they read; otherwise the answer is <c>-ERR syntax error</c>.</returns>
    public static bool TryRead(IReadOnlyList<byte[]> arguments, out RedisExpireCondition condition)
    {
        condition = RedisExpireCondition.None;
        for (var index = 3; index < arguments.Count; index++)
        {
            var option = RedisCommandTable.UpperCaseName(arguments[index]) switch
            {
                "NX" => RedisExpireCondition.OnlyWithoutExpiry,
                "XX" => RedisExpireCondition.OnlyWithExpiry,
                "GT" => RedisExpireCondition.OnlyLater,
                "LT" => RedisExpireCondition.OnlyEarlier,
                _ => RedisExpireCondition.None,
            };
            condition |= option;
            if (option == RedisExpireCondition.None || Conflicts(condition))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether <paramref name="condition"/> lets a key expiring at <paramref name="current"/> take <paramref name="expiresAt"/>.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="current">The key's expiry, or <see langword="null"/> for none.</param>
    /// <param name="expiresAt">The new expiry.</param>
    /// <returns>Whether to set it.</returns>
    public static bool Allows(RedisExpireCondition condition, long? current, long expiresAt) =>
        AllowsByPresence(condition, current) && AllowsByOrder(condition, current, expiresAt);

    private static bool AllowsByPresence(RedisExpireCondition condition, long? current) =>
        condition.HasFlag(RedisExpireCondition.OnlyWithoutExpiry)
            ? current is null
            : !condition.HasFlag(RedisExpireCondition.OnlyWithExpiry) || current is not null;

    // A key without expiry expires never: no time is later, and every time is earlier.
    private static bool AllowsByOrder(RedisExpireCondition condition, long? current, long expiresAt) =>
        condition.HasFlag(RedisExpireCondition.OnlyLater)
            ? expiresAt > current
            : !condition.HasFlag(RedisExpireCondition.OnlyEarlier) || current is null || expiresAt < current;

    private static bool Conflicts(RedisExpireCondition condition) =>
        (condition.HasFlag(RedisExpireCondition.OnlyWithoutExpiry) && condition != RedisExpireCondition.OnlyWithoutExpiry)
        || condition.HasFlag(RedisExpireCondition.OnlyLater | RedisExpireCondition.OnlyEarlier);
}
