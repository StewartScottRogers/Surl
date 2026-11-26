using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.LineProtocol;

/// <summary>
/// The <see cref="ILineRedactionRule"/> for the IMAP <c>LOGIN</c> command in a tunnel: on a line
/// whose first or second word is <c>LOGIN</c>, matched without regard to case, it writes everything
/// after the user name (an atom or a quoted string) as <see cref="LineBytesRedactor.SecretMask"/>.
/// A first word <c>LOGIN</c> is a command with no tag; when the second word is <c>LOGIN</c> too, the
/// first may be the tag or the second the user name, so everything after both is masked. When such a
/// line ends in a literal marker (<c>{6}</c> or <c>{6+}</c>) it writes every line that holds the
/// literal's bytes, and the rest of the command on the line that finishes them, as
/// <see cref="LineBytesRedactor.SecretMask"/> (ADR-0182, amendments 9, 16 and 20). Every other line
/// is written as received.
/// </summary>
/// <remarks>
/// Each line is read twice, because the peer's dialect is unknown: with words separated by runs of
/// spaces and tabs, and with words separated by runs of spaces alone, so that a tab inside or before
/// a tag, or standing as the user name, hides no password. It is a <c>LOGIN</c> line when either
/// reading says so, and it is masked from the earlier password of the two; blanks after the user name
/// are logged either way. A marker alone in the password's place is not a secret and is logged. The server's answer to a synchronizing literal is not awaited, so after a
/// refused one the next lines are withheld until the announced count is used up: that loses log
/// detail and never reveals a secret. One instance serves one tunnel.
/// </remarks>
public sealed class TunnelImapLoginLineRule : ILineRedactionRule
{
    private const byte Quote = (byte)'"';
    private const byte Backslash = (byte)'\\';
    private const long NoLiteral = -1;

    private static ReadOnlySpan<byte> Blanks => " \t"u8;

    private long _literalBytesPending = NoLiteral;

    /// <summary>
    /// How many bytes ended the line judged next, which a literal that runs past it also counts: 2 for
    /// CR LF, 1 for a bare line feed.
    /// </summary>
    public int LineEndLength { get; set; } = 2;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (_literalBytesPending == NoLiteral)
        {
            RedactCommandLine(line, output);
        }
        else
        {
            RedactLiteralLine(line, output);
        }
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        // A literal's bytes are counted from the client's lines alone.
    }

    // Reads "[<tag>] LOGIN <user> <password>" with words ended by any of the separators, those before
    // the first word skipped. Null when neither of the first two words is LOGIN. When the first is,
    // the line is read as one with no tag, whose password begins no later than a tagged reading's.
    private static int? FindPasswordStart(ReadOnlySpan<byte> line, ReadOnlySpan<byte> separators)
    {
        var firstWordStart = SkipAll(line, 0, separators);
        var secondWordStart = SkipAll(line, firstWordStart + AtomLength(line[firstWordStart..], separators), separators);

        return FindPasswordStartAfterLogin(line, firstWordStart, separators)
            ?? FindPasswordStartAfterLogin(line, secondWordStart, separators);
    }

    // Null when the word at wordStart is not LOGIN; otherwise where the password begins, after the
    // blanks that follow the user name, which is the line's length when nothing follows.
    private static int? FindPasswordStartAfterLogin(
        ReadOnlySpan<byte> line, int wordStart, ReadOnlySpan<byte> separators)
    {
        var argumentsStart = wordStart + AtomLength(line[wordStart..], separators);
        if (!Ascii.EqualsIgnoreCase(line[wordStart..argumentsStart], "LOGIN"u8))
        {
            return null;
        }

        var userStart = SkipAll(line, argumentsStart, separators);
        var fromUser = line[userStart..];
        var userLength = fromUser.StartsWith([Quote]) ? QuotedLength(fromUser) : AtomLength(fromUser, separators);

        return SkipAll(line, userStart + userLength, Blanks);
    }

    // Where the line stops being shown under one reading: its length when that reading holds no secret.
    private static int ShownLength(ReadOnlySpan<byte> line, int? passwordStart) =>
        passwordStart is { } start && start < line.Length && !IsLiteralMarkerAlone(line[start..])
            ? start
            : line.Length;

    private static int SkipAll(ReadOnlySpan<byte> line, int start, ReadOnlySpan<byte> skipped)
    {
        var next = line[start..].IndexOfAnyExcept(skipped);

        return next < 0 ? line.Length : start + next;
    }

    private static int AtomLength(ReadOnlySpan<byte> text, ReadOnlySpan<byte> separators)
    {
        var end = text.IndexOfAny(separators);

        return end < 0 ? text.Length : end;
    }

    private static int QuotedLength(ReadOnlySpan<byte> text)
    {
        for (var index = 1; index < text.Length; index++)
        {
            if (text[index] == Backslash)
            {
                index++;
            }
            else if (text[index] == Quote)
            {
                return index + 1;
            }
        }

        return text.Length;
    }

    private static bool IsLiteralMarkerAlone(ReadOnlySpan<byte> text) =>
        text.LastIndexOf((byte)'{') == 0 && TrailingLiteralLength(text) != NoLiteral;

    // The count of a "{n}" or "{n+}" that ends the text; long.MaxValue for one no long holds.
    private static long TrailingLiteralLength(ReadOnlySpan<byte> text)
    {
        var brace = text.LastIndexOf((byte)'{');
        if (brace < 0 || !text.EndsWith("}"u8))
        {
            return NoLiteral;
        }

        var digits = text[(brace + 1)..^1];
        if (digits.EndsWith("+"u8))
        {
            digits = digits[..^1];
        }

        if (digits.IsEmpty || digits.ContainsAnyExceptInRange((byte)'0', (byte)'9'))
        {
            return NoLiteral;
        }

        return long.TryParse(digits, out var length) ? length : long.MaxValue;
    }

    private void RedactCommandLine(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        var blankSeparated = FindPasswordStart(line, Blanks);
        var spaceSeparated = FindPasswordStart(line, " "u8);
        if (blankSeparated is null && spaceSeparated is null)
        {
            output.Write(line);
            return;
        }

        _literalBytesPending = TrailingLiteralLength(line);
        var shownLength = Math.Min(ShownLength(line, blankSeparated), ShownLength(line, spaceSeparated));
        output.Write(line[..shownLength]);
        if (shownLength < line.Length)
        {
            output.Write(LineBytesRedactor.SecretMask);
        }
    }

    private void RedactLiteralLine(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        output.Write(LineBytesRedactor.SecretMask);
        var lineWithEnd = line.Length + (long)LineEndLength;

        // A line that finishes the literal may announce the command's next one after it.
        _literalBytesPending = _literalBytesPending >= lineWithEnd
            ? _literalBytesPending - lineWithEnd
            : TrailingLiteralLength(line[(int)Math.Min(_literalBytesPending, line.Length)..]);
    }
}
