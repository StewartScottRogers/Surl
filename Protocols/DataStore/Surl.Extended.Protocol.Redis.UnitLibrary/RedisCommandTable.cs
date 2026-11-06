using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Finds the command a client sent, case-insensitively (the RESP page), and refuses an unknown
/// command or subcommand and a wrong argument count as Redis does, before any login check
/// (ADR-0106 decisions 1 and 5).
/// </summary>
internal static class RedisCommandTable
{
    // No served name is longer; a longer one is unknown without being decoded.
    private const int LongestName = 16;

    private static readonly FrozenDictionary<string, RedisCommand> Commands =
        RedisConnectionCommands.Commands.Concat(RedisKeyCommands.Commands).Concat(RedisStringCommands.Commands)
            .Concat(RedisHashCommands.Commands).Concat(RedisListCommands.Commands).Concat(RedisSetCommands.Commands).Concat(RedisPubSubCommands.Commands)
            .ToFrozenDictionary(command => command.Name.ToUpperInvariant());

    private static readonly FrozenDictionary<string, RedisCommandContainer> Containers =
        new[] { RedisConnectionCommands.Client }.ToFrozenDictionary(container => container.Name.ToUpperInvariant());

    /// <summary>Finds the command <paramref name="arguments"/> names.</summary>
    /// <param name="arguments">The command's name then its arguments.</param>
    /// <param name="command">The command, when found and its argument count suits it.</param>
    /// <param name="refusal">Otherwise the error to answer.</param>
    /// <returns>Whether the command was found and suits its argument count.</returns>
    public static bool TryFind(
        IReadOnlyList<byte[]> arguments,
        [NotNullWhen(true)] out RedisCommand? command,
        [NotNullWhen(false)] out RespSimpleError? refusal)
    {
        var name = UpperCaseName(arguments[0]);
        if (Commands.TryGetValue(name, out var found))
        {
            return Suits(found, arguments.Count, out command, out refusal);
        }

        if (Containers.TryGetValue(name, out var container))
        {
            return TryFindSubcommand(container, arguments, out command, out refusal);
        }

        command = null;
        refusal = RedisErrorReplies.UnknownCommand(arguments);
        return false;
    }

    private static bool TryFindSubcommand(
        RedisCommandContainer container,
        IReadOnlyList<byte[]> arguments,
        [NotNullWhen(true)] out RedisCommand? command,
        [NotNullWhen(false)] out RespSimpleError? refusal)
    {
        command = null;
        if (!container.AcceptsArgumentCount(arguments.Count))
        {
            refusal = RedisErrorReplies.WrongNumberOfArguments(container.Name);
            return false;
        }

        if (!container.Subcommands.TryGetValue(UpperCaseName(arguments[1]), out var subcommand))
        {
            refusal = RedisErrorReplies.UnknownSubcommand(arguments[1], container.Name);
            return false;
        }

        return Suits(subcommand, arguments.Count, out command, out refusal);
    }

    private static bool Suits(
        RedisCommand found,
        int argumentCount,
        [NotNullWhen(true)] out RedisCommand? command,
        [NotNullWhen(false)] out RespSimpleError? refusal)
    {
        if (found.AcceptsArgumentCount(argumentCount))
        {
            command = found;
            refusal = null;
            return true;
        }

        command = null;
        refusal = RedisErrorReplies.WrongNumberOfArguments(found.Name);
        return false;
    }

    /// <summary>The name in upper case, as a key; ASCII only, so a byte outside it matches nothing.</summary>
    internal static string UpperCaseName(byte[] name) =>
        name.Length > LongestName ? string.Empty : Encoding.ASCII.GetString(name).ToUpperInvariant();
}
