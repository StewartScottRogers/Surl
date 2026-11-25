using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// The <see cref="ILineRedactionRule"/> for the IMAP <c>LOGIN</c> command in a SOCKS tunnel: on a
/// line whose second word is <c>LOGIN</c>, matched without regard to case, it writes everything after
/// the user name (an atom or a quoted string) as <see cref="LineBytesRedactor.SecretMask"/>. When
/// such a line ends in a literal marker (<c>{6}</c> or <c>{6+}</c>) it writes every line that holds
/// the literal's bytes, and the rest of the command on the line that finishes them, as
/// <see cref="LineBytesRedactor.SecretMask"/> (ADR-0182, amendment 9). Every other line is written as
/// received.
/// </summary>
/// <remarks>
/// A marker alone in the password's place is not a secret and is logged. The server's answer to a
/// synchronizing literal is not awaited, so after a refused one the next lines are withheld until the
/// announced count is used up: that loses log detail and never reveals a secret.
/// </remarks>
internal sealed class SocksTunnelImapLoginLineRule : ILineRedactionRule
{
    private const byte Space = (byte)' ';
    private const byte Quote = (byte)'"';
    private const byte Backslash = (byte)'\\';
    private const long NoLiteral = -1;

    private long literalBytesPending = NoLiteral;

    /// <summary>
    /// How many bytes ended the line judged next, which a literal that runs past it also counts: 2 for
    /// CR LF, 1 for a bare line feed.
    /// </summary>
    public int LineEndLength { get; set; } = 2;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (literalBytesPending == NoLiteral)
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

    private void RedactCommandLine(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        if (!TryFindLoginArguments(line, out int argumentsStart))
        {
            output.Write(line);
            return;
        }

        int passwordStart = FindPasswordStart(line, argumentsStart);
        literalBytesPending = TrailingLiteralLength(line);
        if (passwordStart == line.Length || IsLiteralMarkerAlone(line[passwordStart..]))
        {
            output.Write(line);
            return;
        }

        output.Write(line[..passwordStart]);
        output.Write(LineBytesRedactor.SecretMask);
    }

    private void RedactLiteralLine(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        output.Write(LineBytesRedactor.SecretMask);
        long lineWithEnd = line.Length + (long)LineEndLength;

        // A line that finishes the literal may announce the command's next one after it.
        literalBytesPending = literalBytesPending >= lineWithEnd
            ? literalBytesPending - lineWithEnd
            : TrailingLiteralLength(line[(int)Math.Min(literalBytesPending, line.Length)..]);
    }

    // The arguments begin right after "<tag> LOGIN", however many spaces follow the tag.
    private static bool TryFindLoginArguments(ReadOnlySpan<byte> line, out int argumentsStart)
    {
        int tagLength = AtomLength(line);
        int wordStart = SkipSpaces(line, tagLength);
        argumentsStart = wordStart + AtomLength(line[wordStart..]);

        return Ascii.EqualsIgnoreCase(line[wordStart..argumentsStart], "LOGIN"u8);
    }

    // After the user name and the spaces that follow it; the line's length when nothing follows.
    private static int FindPasswordStart(ReadOnlySpan<byte> line, int argumentsStart)
    {
        int userStart = SkipSpaces(line, argumentsStart);
        ReadOnlySpan<byte> fromUser = line[userStart..];
        int userLength = fromUser.StartsWith([Quote]) ? QuotedLength(fromUser) : AtomLength(fromUser);

        return SkipSpaces(line, userStart + userLength);
    }

    private static int SkipSpaces(ReadOnlySpan<byte> line, int start)
    {
        int next = line[start..].IndexOfAnyExcept(Space);

        return next < 0 ? line.Length : start + next;
    }

    private static int AtomLength(ReadOnlySpan<byte> text)
    {
        int space = text.IndexOf(Space);

        return space < 0 ? text.Length : space;
    }

    private static int QuotedLength(ReadOnlySpan<byte> text)
    {
        for (int index = 1; index < text.Length; index++)
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
        int brace = text.LastIndexOf((byte)'{');
        if (brace < 0 || !text.EndsWith("}"u8))
        {
            return NoLiteral;
        }

        ReadOnlySpan<byte> digits = text[(brace + 1)..^1];
        if (digits.EndsWith("+"u8))
        {
            digits = digits[..^1];
        }

        if (digits.IsEmpty || digits.ContainsAnyExceptInRange((byte)'0', (byte)'9'))
        {
            return NoLiteral;
        }

        return long.TryParse(digits, out long length) ? length : long.MaxValue;
    }
}
