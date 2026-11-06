using System.Text;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Splits an <c>exec</c> command into words by the quoting ADR-0054 decision 2 accepts: words
/// separated by spaces and tabs, each a concatenation of single-quoted runs, double-quoted runs
/// (where <c>\</c> escapes only <c>"</c>, <c>\</c>, <c>$</c> and <c>`</c>), a <c>\</c> escaping
/// the next character, and bare characters other than those a shell would expand. Surl runs no
/// shell, so anything a shell would expand is refused rather than read.
/// </summary>
internal static class SshCommandWords
{
    private const string UnquotedSpecials = ";&|<>()$`*?[]{}~#!";

    private const string DoubleQuotedEscapes = "\"\\$`";

    /// <summary>
    /// Splits <paramref name="command"/> into its words, quotes removed.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="refusal">Why the command was refused, <c>unquoted &lt;character&gt;</c>; empty when it was not.</param>
    /// <returns>The words in order, or <see langword="null"/> when the command was refused.</returns>
    public static List<string>? Split(string command, out string refusal)
    {
        var words = new List<string>();
        var position = SkipBlanks(command, 0);
        refusal = string.Empty;
        while (position < command.Length)
        {
            var word = new StringBuilder();
            position = ReadWord(command, position, word, ref refusal);
            if (position < 0)
            {
                return null;
            }

            words.Add(word.ToString());
            position = SkipBlanks(command, position);
        }

        return words;
    }

    private static int SkipBlanks(string command, int position)
    {
        while (position < command.Length && IsBlank(command[position]))
        {
            position++;
        }

        return position;
    }

    private static bool IsBlank(char character) => character is ' ' or '\t';

    // Reads one word from position into word; returns where it ended, or -1 with the refusal.
    private static int ReadWord(string command, int position, StringBuilder word, ref string refusal)
    {
        while (position >= 0 && position < command.Length && !IsBlank(command[position]))
        {
            position = ReadRun(command, position, word, ref refusal);
        }

        return position;
    }

    // Reads one quoted run, escaped character or bare character; returns where it ended, or -1.
    private static int ReadRun(string command, int position, StringBuilder word, ref string refusal) => command[position] switch
    {
        '\'' => ReadSingleQuoted(command, position + 1, word, ref refusal),
        '"' => ReadDoubleQuoted(command, position + 1, word, ref refusal),
        '\\' => ReadEscaped(command, position + 1, word, ref refusal),
        _ => ReadBare(command, position, word, ref refusal),
    };

    private static int ReadSingleQuoted(string command, int position, StringBuilder word, ref string refusal)
    {
        var close = command.IndexOf('\'', position);
        if (close < 0)
        {
            return Refuse('\'', ref refusal);
        }

        word.Append(command, position, close - position);

        return close + 1;
    }

    private static int ReadDoubleQuoted(string command, int position, StringBuilder word, ref string refusal)
    {
        while (position >= 0 && position < command.Length && command[position] != '"')
        {
            position = ReadDoubleQuotedCharacter(command, position, word, ref refusal);
        }

        if (position < 0)
        {
            return position;
        }

        return position < command.Length ? position + 1 : Refuse('"', ref refusal);
    }

    // Inside double quotes a backslash escapes only ", \, $ and `, and an unescaped $ or ` is refused.
    private static int ReadDoubleQuotedCharacter(string command, int position, StringBuilder word, ref string refusal)
    {
        var character = command[position];
        if (character is '$' or '`')
        {
            return Refuse(character, ref refusal);
        }

        if (character == '\\' && IsDoubleQuotedEscape(command, position + 1))
        {
            word.Append(command[position + 1]);

            return position + 2;
        }

        word.Append(character);

        return position + 1;
    }

    private static bool IsDoubleQuotedEscape(string command, int position) =>
        position < command.Length && DoubleQuotedEscapes.Contains(command[position], StringComparison.Ordinal);

    private static int ReadEscaped(string command, int position, StringBuilder word, ref string refusal)
    {
        if (position == command.Length)
        {
            return Refuse('\\', ref refusal);
        }

        word.Append(command[position]);

        return position + 1;
    }

    private static int ReadBare(string command, int position, StringBuilder word, ref string refusal)
    {
        var character = command[position];
        if (UnquotedSpecials.Contains(character, StringComparison.Ordinal))
        {
            return Refuse(character, ref refusal);
        }

        word.Append(character);

        return position + 1;
    }

    private static int Refuse(char character, ref string refusal)
    {
        refusal = $"unquoted {character}";

        return -1;
    }
}
