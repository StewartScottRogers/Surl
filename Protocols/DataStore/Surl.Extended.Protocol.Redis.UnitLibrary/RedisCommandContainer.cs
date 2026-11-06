using System.Collections.Frozen;

namespace Surl.Extended.Protocol.Redis;

/// <summary>A command whose second argument names a subcommand, as <c>CLIENT</c>.</summary>
/// <param name="Name">The command's name in lower case.</param>
/// <param name="Arity">Redis's arity of the command itself (see <see cref="RedisCommand.Arity"/>).</param>
/// <param name="Subcommands">The subcommands, by upper-case name.</param>
internal sealed record RedisCommandContainer(string Name, int Arity, FrozenDictionary<string, RedisCommand> Subcommands)
{
    /// <summary>Whether <paramref name="count"/> arguments, the name included, suit <see cref="Arity"/>.</summary>
    /// <param name="count">The argument count.</param>
    /// <returns>Whether the count suits.</returns>
    public bool AcceptsArgumentCount(int count) => RedisArity.Accepts(Arity, count);
}
