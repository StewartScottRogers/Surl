namespace Surl.Extended.Protocol.Redis;

/// <summary>One served command, or one subcommand of a <see cref="RedisCommandContainer"/>.</summary>
/// <param name="Name">The name errors give it, in lower case: <c>ping</c>, <c>client|setname</c>.</param>
/// <param name="Arity">
/// Redis's arity: the exact argument count, the name included, when positive; the least, negated,
/// when negative.
/// </param>
/// <param name="IsServedBeforeLogin">Whether it runs on a connection that must still log in (ADR-0106 decision 4).</param>
/// <param name="Answer">Answers it.</param>
internal sealed record RedisCommand(string Name, int Arity, bool IsServedBeforeLogin, RedisCommandAnswer Answer)
{
    /// <summary>Whether <paramref name="count"/> arguments, the name included, suit <see cref="Arity"/>.</summary>
    /// <param name="count">The argument count.</param>
    /// <returns>Whether the count suits.</returns>
    public bool AcceptsArgumentCount(int count) => RedisArity.Accepts(Arity, count);
}
