using System.Globalization;
using System.Text;

namespace Surl.Protocol.Ftp;

/// <summary>
/// The <c>&lt;servercmd&gt;</c> lines of an upstream test case that the FTP server half
/// implements, read once (FILEFORMAT.md at tag <c>curl-8_21_0</c>, "For FTP/SMTP/POP/IMAP";
/// ADR-0080 decision 2). Not safe for concurrent calls: <see cref="FtpUpstreamTestCaseReplies"/>
/// takes its lock around <see cref="TakeReply"/>.
/// </summary>
/// <remarks>
/// <c>REPLY [command] [response]</c> and <c>REPLYLF</c> replace the server's answer to
/// <c>[command]</c> with <c>[response]</c>, read as a Perl double-quoted string, ended with CRLF or
/// LF; the command <c>welcome</c> replaces the greeting. <c>COUNT [command] [num]</c> keeps a
/// <c>REPLY</c> for <c>[num]</c> answers only. <c>RETRNOSIZE</c> leaves the size out of
/// <c>RETR</c>'s <c>150</c>, <c>RETRSIZE [size]</c> puts that size in it, <c>NOSAVE</c> keeps no
/// upload, and <c>STOR [msg]</c> ends an upload with <c>[msg]</c> in place of <c>226</c>.
/// <c>DELAY</c>, <c>RETRWEIRDO</c>, <c>SLOWDOWN</c>, <c>SLOWDOWNDATA</c>, <c>PASVBADIP</c> and
/// the older <c>NODATACONN</c> forms are not implemented: a case using one is skipped
/// <c>servercmd-unsupported:&lt;command&gt;</c>.
/// </remarks>
internal sealed class FtpUpstreamTestCaseServerCommands
{
    /// <summary>The <c>REPLY</c> command name that stands for the greeting.</summary>
    public const string WelcomeCommand = "welcome";

    private const string CrLf = "\r\n";

    // What each command does to the commands read so far, given its line and its argument.
    private static readonly Dictionary<string, Action<FtpUpstreamTestCaseServerCommands, string, string>> Appliers = new(StringComparer.Ordinal)
    {
        ["Testnum"] = static (_, _, _) => { },
        ["REPLY"] = static (commands, line, argument) => commands.AddReply(line, argument, CrLf),
        ["REPLYLF"] = static (commands, line, argument) => commands.AddReply(line, argument, "\n"),
        ["COUNT"] = static (commands, line, argument) => commands.AddCount(line, argument),
        ["RETRNOSIZE"] = static (commands, _, _) => commands.OmitsRetrieveSize = true,
        ["RETRSIZE"] = static (commands, line, argument) => commands.RetrieveSize = Number(line, argument),
        ["NOSAVE"] = static (commands, _, _) => commands.DiscardsUploads = true,
        ["STOR"] = static (commands, _, argument) => commands.StoreReply = argument,
    };

    // The letters that escape a control character; a backslash before any other character stands for it.
    private static readonly Dictionary<char, char> ControlEscapes = new() { ['r'] = '\r', ['n'] = '\n', ['t'] = '\t' };

    private readonly Dictionary<string, string> replies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> counts = new(StringComparer.Ordinal);

    private FtpUpstreamTestCaseServerCommands()
    {
    }

    /// <summary>
    /// The first words of the lines <see cref="Parse"/> accepts. <c>Testnum</c> is the line
    /// upstream's runtests writes first; it names the case, which a server half already knows.
    /// </summary>
    public static IReadOnlySet<string> Supported { get; } = new HashSet<string>(Appliers.Keys, StringComparer.Ordinal);

    /// <summary><c>RETRNOSIZE</c>: <c>RETR</c>'s <c>150</c> names no size.</summary>
    public bool OmitsRetrieveSize { get; private set; }

    /// <summary><c>RETRSIZE [size]</c>: the size <c>RETR</c>'s <c>150</c> names, whatever the file's.</summary>
    public long? RetrieveSize { get; private set; }

    /// <summary><c>NOSAVE</c>: an upload is read and thrown away.</summary>
    public bool DiscardsUploads { get; private set; }

    /// <summary><c>STOR [msg]</c>: the reply that ends an upload in place of <c>226</c>.</summary>
    public string? StoreReply { get; private set; }

    /// <summary>
    /// Reads <paramref name="lines"/>; blank lines and lines starting <c>#</c> are skipped.
    /// </summary>
    /// <param name="lines">The case's <c>&lt;servercmd&gt;</c> lines.</param>
    /// <returns>The commands.</returns>
    /// <exception cref="NotSupportedException">A line names a command the server half does not implement.</exception>
    /// <exception cref="FormatException">A <c>REPLY</c> names no command, or a <c>COUNT</c> or <c>RETRSIZE</c> has no non-negative number.</exception>
    public static FtpUpstreamTestCaseServerCommands Parse(IEnumerable<string> lines)
    {
        var commands = new FtpUpstreamTestCaseServerCommands();
        foreach (var line in lines.Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#')))
        {
            commands.Apply(line);
        }

        return commands;
    }

    /// <summary>
    /// The scripted answer to <paramref name="command"/>, with its line ending, spending one of
    /// its <c>COUNT</c> uses.
    /// </summary>
    /// <param name="command">The command in upper case, or <see cref="WelcomeCommand"/>.</param>
    /// <returns>The answer, or <see langword="null"/> when the server answers it itself.</returns>
    public string? TakeReply(string command)
    {
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
            throw new NotSupportedException($"The FTP server half does not implement the server command '{word}' (servercmd-unsupported:{word}).");
        }

        apply(this, line, argument);
    }

    // REPLY [command] [response]: exactly one space after the command, the rest is the response.
    private void AddReply(string line, string argument, string lineEnding)
    {
        var (command, response) = SplitFirstWord(argument);
        if (command.Length == 0)
        {
            throw new FormatException($"The server command '{line}' names no command.");
        }

        replies[CommandKey(command)] = ReadPerlDoubleQuoted(response) + lineEnding;
    }

    private void AddCount(string line, string argument)
    {
        var (command, number) = SplitFirstWord(argument);
        counts[CommandKey(command)] = Number(line, number);
    }

    private static string CommandKey(string command) =>
        command == WelcomeCommand ? command : command.ToUpperInvariant();

    private static (string Word, string Remainder) SplitFirstWord(string text)
    {
        var separator = text.IndexOfAny([' ', '\t']);

        return separator < 0 ? (text, string.Empty) : (text[..separator], text[(separator + 1)..]);
    }

    private static long Number(string line, string argument) =>
        long.TryParse(argument.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new FormatException($"The server command '{line}' needs a non-negative whole number.");
}
