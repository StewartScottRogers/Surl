using System.Text;

namespace Surl.Protocol.Pop3;

/// <summary>
/// One POP3 command line split into its command and argument (ADR-0056, decision 1): the command
/// is the line's first word in capitals, the argument the rest of the line after one space.
/// </summary>
/// <param name="Verb">The command, in capitals.</param>
/// <param name="Argument">The bytes after the first space, or <see langword="null"/> when the
/// line has no space or nothing follows it.</param>
internal sealed record Pop3CommandLine(string Verb, byte[]? Argument)
{
    /// <summary>
    /// Splits <paramref name="line"/>, a command line without its CRLF.
    /// </summary>
    /// <param name="line">The line's bytes.</param>
    /// <param name="command">The command, when the line is one.</param>
    /// <returns><see langword="false"/> for a line that is no command: one holding a bare CR
    /// or LF, one that is empty or starts with a space, or one with a byte outside 0x21 to
    /// 0x7E before its first space.</returns>
    public static bool TryParse(byte[] line, out Pop3CommandLine? command)
    {
        var space = Array.IndexOf(line, (byte)' ');
        var verb = space < 0 ? line : line[..space];
        command = IsCommand(line, verb)
            ? new Pop3CommandLine(Encoding.ASCII.GetString(verb).ToUpperInvariant(), ArgumentAfter(line, space))
            : null;
        return command is not null;
    }

    /// <summary>
    /// The argument split at single spaces (ADR-0056, decision 1).
    /// </summary>
    /// <param name="words">The words, none when there is no argument.</param>
    /// <returns><see langword="false"/> when the argument holds an empty word (two spaces in a
    /// row, or a space at its end) or a byte outside 0x21 to 0x7E other than the spaces.</returns>
    public bool TrySplitArguments(out string[] words)
    {
        words = Argument is null ? [] : Encoding.ASCII.GetString(Argument).Split(' ');
        return Argument is null || (!Argument.AsSpan().ContainsAnyExceptInRange((byte)0x20, (byte)0x7E) && !words.Contains(string.Empty));
    }

    private static bool IsCommand(byte[] line, byte[] verb) =>
        verb.Length > 0 && !verb.AsSpan().ContainsAnyExceptInRange((byte)0x21, (byte)0x7E) && line.AsSpan().IndexOfAny((byte)'\r', (byte)'\n') < 0;

    private static byte[]? ArgumentAfter(byte[] line, int space) =>
        space < 0 || space == line.Length - 1 ? null : line[(space + 1)..];
}
