using System.Globalization;
using System.Text;

namespace Surl.LineProtocol;

/// <summary>
/// The <c>&lt;servercmd&gt;</c> lines of an upstream test case that the IMAP, POP3 and SMTP
/// server halves implement, read once (FILEFORMAT.md at tag <c>curl-8_21_0</c>, "For
/// FTP/SMTP/POP/IMAP"; ADR-0080 decision 2). Not safe for concurrent calls:
/// <see cref="MailUpstreamTestCaseReplies"/> takes its lock around <see cref="TakeReply"/>.
/// </summary>
/// <remarks>
/// <para>
/// <c>REPLY [command] [response]</c> and <c>REPLYLF</c> replace the server's answer to
/// <c>[command]</c> with <c>[response]</c>, read as a Perl double-quoted string, ended with CRLF or
/// LF; <c>[command]</c> may be empty (an empty SASL response), and <c>welcome</c> replaces the
/// greeting. <c>REPLY "[command] [arguments]" [response]</c> replaces the answer to that exact
/// command text only, ended with CRLF. Commands match case for case, as upstream's server
/// matches them, since a SASL response's base64 is a command there. <c>COUNT [command] [num]</c>
/// keeps a <c>REPLY</c> for <c>[num]</c> answers only.
/// </para>
/// <para>
/// <c>CAPA [capabilities]</c> lists what IMAP's <c>CAPABILITY</c>, POP3's <c>CAPA</c> and SMTP's
/// <c>EHLO</c> offer - space-separated, a double-quoted one keeping its spaces - and <c>AUTH
/// [mechanisms]</c> the SASL mechanisms they offer. <c>POSTFETCH [text]</c> is written after the
/// message in an IMAP <c>FETCH</c> response, before its closing parenthesis.
/// </para>
/// <para>
/// <c>DELAY</c>, <c>SLOWDOWN</c>, <c>SLOWDOWNDATA</c>, <c>NOSAVE</c> and FTP's own commands are not
/// implemented: a case using one is skipped <c>servercmd-unsupported:&lt;command&gt;</c>.
/// </para>
/// </remarks>
internal sealed class MailUpstreamTestCaseServerCommands
{
    /// <summary>The <c>REPLY</c> command name that stands for the greeting.</summary>
    public const string WelcomeCommand = "welcome";

    private const string CrLf = "\r\n";

    // What each command does to the commands read so far, given its line and its argument.
    private static readonly Dictionary<string, Action<MailUpstreamTestCaseServerCommands, string, string>> Appliers = new(StringComparer.Ordinal)
    {
        ["Testnum"] = static (_, _, _) => { },
        ["REPLY"] = static (commands, _, argument) => commands.AddReply(argument, CrLf),
        ["REPLYLF"] = static (commands, _, argument) => commands.AddReply(argument, "\n"),
        ["COUNT"] = static (commands, line, argument) => commands.AddCount(line, argument),
        ["CAPA"] = static (commands, _, argument) => commands.Capabilities = SplitCapabilities(argument),
        ["AUTH"] = static (commands, _, argument) => commands.AuthMechanisms = [.. argument.Split(' ', StringSplitOptions.RemoveEmptyEntries)],
        ["POSTFETCH"] = static (commands, _, argument) => commands.PostFetch = argument,
    };

    // The letters that escape a control character; a backslash before any other character stands for it.
    private static readonly Dictionary<char, char> ControlEscapes = new() { ['r'] = '\r', ['n'] = '\n', ['t'] = '\t' };

    private readonly Dictionary<string, string> replies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> fullTextReplies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> counts = new(StringComparer.Ordinal);

    private MailUpstreamTestCaseServerCommands()
    {
    }

    /// <summary>
    /// The first words of the lines <see cref="Parse"/> accepts. <c>Testnum</c> is the line
    /// upstream's runtests writes first; it names the case, which a server half already knows.
    /// </summary>
    public static IReadOnlySet<string> Supported { get; } = new HashSet<string>(Appliers.Keys, StringComparer.Ordinal);

    /// <summary><c>CAPA</c>: the capabilities offered, in the case's order; none without it.</summary>
    public IReadOnlyList<string> Capabilities { get; private set; } = [];

    /// <summary><c>AUTH</c>: the SASL mechanisms offered, in the case's order; none without it.</summary>
    public IReadOnlyList<string> AuthMechanisms { get; private set; } = [];

    /// <summary><c>POSTFETCH</c>: the text written after a fetched message; empty without it.</summary>
    public string PostFetch { get; private set; } = string.Empty;

    /// <summary>
    /// Reads <paramref name="lines"/>; lines that are blank or start with <c>#</c> are skipped,
    /// and a line's leading blanks and line ending are not part of it.
    /// </summary>
    /// <param name="lines">The case's <c>&lt;servercmd&gt;</c> lines.</param>
    /// <returns>The commands.</returns>
    /// <exception cref="NotSupportedException">A line names a command the mail server halves do not implement.</exception>
    /// <exception cref="FormatException">A <c>COUNT</c> has no non-negative number, or a quoted <c>REPLY</c> command is not closed.</exception>
    public static MailUpstreamTestCaseServerCommands Parse(IEnumerable<string> lines)
    {
        var commands = new MailUpstreamTestCaseServerCommands();
        foreach (var line in lines.Select(line => line.TrimStart().TrimEnd('\r', '\n')).Where(line => line.Trim().Length > 0 && !line.StartsWith('#')))
        {
            commands.Apply(line);
        }

        return commands;
    }

    /// <summary>
    /// The scripted answer to a command, with its line ending: the one for its exact text first,
    /// then the one for its command word, spending one of that word's <c>COUNT</c> uses.
    /// </summary>
    /// <param name="commandText">The command as sent, without an IMAP tag: <c>AUTH PLAIN</c>, or a SASL response's base64.</param>
    /// <param name="command">Its command word as sent, or <see cref="WelcomeCommand"/> for the greeting.</param>
    /// <returns>The answer, or <see langword="null"/> when the server answers it itself.</returns>
    public string? TakeReply(string commandText, string command)
    {
        if (fullTextReplies.TryGetValue(commandText, out var fullTextReply))
        {
            return fullTextReply;
        }

        if (!replies.TryGetValue(command, out var reply))
        {
            return null;
        }

        if (counts.TryGetValue(command, out var remaining))
        {
            SpendOneUse(command, remaining);
        }

        return reply;
    }

    /// <summary>
    /// The greeting <c>REPLY welcome</c> gives, ended with CRLF, once: upstream's server clears it
    /// after the first connection.
    /// </summary>
    /// <returns>The greeting, or <see langword="null"/> when the server greets as it does itself.</returns>
    public string? TakeWelcome()
    {
        if (!replies.Remove(WelcomeCommand, out var welcome))
        {
            return null;
        }

        return welcome.EndsWith(CrLf, StringComparison.Ordinal) ? welcome : welcome + CrLf;
    }

    /// <summary>
    /// Reads a Perl double-quoted string's escapes: <c>\r</c>, <c>\n</c>, <c>\t</c>, <c>\xHH</c>,
    /// and a backslash before any other character standing for that character.
    /// </summary>
    /// <param name="text">The string as the case writes it.</param>
    /// <returns>The characters it stands for.</returns>
    internal static string ReadPerlDoubleQuoted(string text)
    {
        var read = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\\' || index == text.Length - 1)
            {
                read.Append(text[index]);
                continue;
            }

            index++;
            index = AppendEscape(read, text, index);
        }

        return read.ToString();
    }

    // Appends what the escape whose letter is at index stands for; returns the index of its last character.
    private static int AppendEscape(StringBuilder read, string text, int index)
    {
        if (text[index] == 'x')
        {
            return AppendHexEscape(read, text, index);
        }

        read.Append(ControlEscapes.GetValueOrDefault(text[index], text[index]));
        return index;
    }

    // \x takes up to two hexadecimal digits, as Perl reads it; none is the character 0.
    private static int AppendHexEscape(StringBuilder read, string text, int index)
    {
        var digits = 0;
        while (digits < 2 && index + digits + 1 < text.Length && char.IsAsciiHexDigit(text[index + digits + 1]))
        {
            digits++;
        }

        read.Append(digits == 0 ? '\0' : (char)int.Parse(text.AsSpan(index + 1, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
        return index + digits;
    }

    // Space-separated, a double-quoted capability keeping its spaces and losing its quotes.
    private static string[] SplitCapabilities(string argument)
    {
        var capabilities = new List<string>();
        var current = new StringBuilder();
        var isQuoted = false;
        foreach (var character in argument)
        {
            if (character == '"')
            {
                isQuoted = !isQuoted;
            }
            else if (character == ' ' && !isQuoted)
            {
                AddCapability(capabilities, current);
            }
            else
            {
                current.Append(character);
            }
        }

        AddCapability(capabilities, current);
        return [.. capabilities];
    }

    private static void AddCapability(List<string> capabilities, StringBuilder current)
    {
        if (current.Length > 0)
        {
            capabilities.Add(current.ToString());
            current.Clear();
        }
    }

    private void SpendOneUse(string command, long remaining)
    {
        if (remaining > 1)
        {
            counts[command] = remaining - 1;
            return;
        }

        replies.Remove(command);
        counts.Remove(command);
    }

    private void Apply(string line)
    {
        var (word, argument) = SplitFirstWord(line);

        if (!Appliers.TryGetValue(word, out var apply))
        {
            throw new NotSupportedException($"The mail server halves do not implement the server command '{word}' (servercmd-unsupported:{word}).");
        }

        apply(this, line, argument);
    }

    // REPLY [command] [response], exactly one space after the command, which may be empty; or
    // REPLY "[command text]" [response] for that exact text.
    private void AddReply(string argument, string lineEnding)
    {
        if (argument.StartsWith('"'))
        {
            AddFullTextReply(argument);
            return;
        }

        var (command, response) = SplitFirstWord(argument);
        replies[command] = ReadPerlDoubleQuoted(response) + lineEnding;
    }

    // Upstream sends a full-text reply with CRLF, whichever REPLY form named it.
    private void AddFullTextReply(string argument)
    {
        var closing = argument.IndexOf("\" ", 1, StringComparison.Ordinal);
        if (closing < 0)
        {
            throw new FormatException($"The server command 'REPLY {argument}' does not close its quoted command.");
        }

        fullTextReplies[argument[1..closing]] = ReadPerlDoubleQuoted(argument[(closing + 2)..]) + CrLf;
    }

    private void AddCount(string line, string argument)
    {
        var (command, number) = SplitFirstWord(argument);
        counts[command] = long.TryParse(number.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            ? count
            : throw new FormatException($"The server command '{line}' needs a non-negative whole number.");
    }

    private static (string Word, string Remainder) SplitFirstWord(string text)
    {
        var separator = text.IndexOf(' ');

        return separator < 0 ? (text, string.Empty) : (text[..separator], text[(separator + 1)..]);
    }
}
