using System.Text;

namespace Surl.Protocol.Smtp;

/// <summary>
/// One SMTP command line split into its command and argument (ADR-0053, decision 1): the
/// command is the line's first word in capitals, the argument the rest of the line after one
/// space.
/// </summary>
/// <param name="Verb">The command, in capitals.</param>
/// <param name="Argument">The bytes after the first space, or <see langword="null"/> when the
/// line has no space or nothing follows it.</param>
internal sealed record SmtpCommandLine(string Verb, byte[]? Argument)
{
    /// <summary>
    /// Splits <paramref name="line"/>, a command line without its CRLF.
    /// </summary>
    /// <param name="line">The line's bytes.</param>
    /// <param name="command">The command, when the line is one.</param>
    /// <returns><see langword="false"/> for a line that is no command: one holding a bare CR
    /// or LF, one that is empty or starts with a space, or one with a byte outside 0x21 to
    /// 0x7E before its first space.</returns>
    public static bool TryParse(byte[] line, out SmtpCommandLine? command)
    {
        var space = Array.IndexOf(line, (byte)' ');
        var verb = space < 0 ? line : line[..space];
        command = IsCommand(line, verb)
            ? new SmtpCommandLine(Encoding.ASCII.GetString(verb).ToUpperInvariant(), ArgumentAfter(line, space))
            : null;
        return command is not null;
    }

    private static bool IsCommand(byte[] line, byte[] verb) =>
        line.AsSpan().IndexOfAny((byte)'\r', (byte)'\n') < 0 && verb.Length > 0 && !verb.AsSpan().ContainsAnyExceptInRange((byte)0x21, (byte)0x7E);

    private static byte[]? ArgumentAfter(byte[] line, int space) =>
        space < 0 || space == line.Length - 1 ? null : line[(space + 1)..];
}
