namespace Surl.Extended.Protocol.Redis;

/// <summary>Redis's arity rule: a positive arity is exact, a negative one the least count, negated.</summary>
internal static class RedisArity
{
    /// <summary>Whether <paramref name="count"/> arguments, the name included, suit <paramref name="arity"/>.</summary>
    /// <param name="arity">The arity.</param>
    /// <param name="count">The argument count.</param>
    /// <returns>Whether the count suits.</returns>
    public static bool Accepts(int arity, int count) => arity >= 0 ? count == arity : count >= -arity;
}
