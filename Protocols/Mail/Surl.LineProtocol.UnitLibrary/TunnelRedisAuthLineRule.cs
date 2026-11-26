using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.LineProtocol;

/// <summary>
/// The <see cref="ILineRedactionRule"/> for the inline Redis <c>AUTH</c> and <c>HELLO ... AUTH</c>
/// lines in a tunnel that <see cref="SaslLineRedactionRule"/> does not withhold (ADR-0183, amendments
/// 8 and 9): an <c>AUTH</c> line whose command word is quoted, or that holds a vertical tab, form feed
/// or carriage return, which Redis counts as white space; an <c>AUTH</c> line with one argument that
/// names no SASL or FTP security mechanism; and the arguments after the <c>AUTH</c> of a <c>HELLO</c>
/// line. It writes the line up to the byte that ends the command word, or that <c>AUTH</c>, then
/// <see cref="LineBytesRedactor.SecretMask"/> and nothing more. Every other line is written as
/// received.
/// </summary>
/// <remarks>
/// Words are the runs of bytes that are not white space. A word is quoted <c>AUTH</c> or <c>HELLO</c>
/// when removing its <c>"</c> and <c>'</c> bytes leaves that word, matched without regard to case; a
/// command word, or a word after <c>HELLO</c>, holding both <c>"</c> and a backslash is taken for
/// <c>AUTH</c> too, because an escape may spell any of its letters. The one argument of an <c>AUTH</c>
/// line keeps the white space it quotes. One instance serves one tunnel.
/// </remarks>
public sealed class TunnelRedisAuthLineRule : ILineRedactionRule
{
    private const byte DoubleQuote = (byte)'"';
    private const byte Backslash = (byte)'\\';

    // What may follow AUTH without being a secret: SASL mechanisms, and FTP's AUTH TLS and SSL (RFC 4217).
    private static readonly string[] MechanismNames =
    [
        "ANONYMOUS", "CRAM-MD5", "DIGEST-MD5", "EXTERNAL", "GSS-SPNEGO", "GSSAPI", "LOGIN", "NTLM", "OAUTHBEARER",
        "PLAIN", "SCRAM-SHA-1", "SCRAM-SHA-1-PLUS", "SCRAM-SHA-256", "SCRAM-SHA-256-PLUS", "SCRAM-SHA-512",
        "SCRAM-SHA-512-PLUS", "XOAUTH2", "SSL", "TLS", "TLS-C", "TLS-P",
    ];

    private static ReadOnlySpan<byte> WhiteSpace => " \t\n\v\f\r"u8;

    // The white space SaslLineRedactionRule does not split words at; a line feed never reaches this rule.
    private static ReadOnlySpan<byte> UnreadSeparators => "\v\f\r"u8;

    private static ReadOnlySpan<byte> Quotes => "\"'"u8;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var maskStart = FindMaskStart(line);
        if (maskStart < 0)
        {
            output.Write(line);
            return;
        }

        output.Write(line[..maskStart]);
        output.Write(LineBytesRedactor.SecretMask);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        // The secret is always on the line that names it, whatever the origin sent.
    }

    // Where the mask begins; -1 when this rule withholds nothing of the line.
    private static int FindMaskStart(ReadOnlySpan<byte> line)
    {
        if (!TryReadFollowedWord(line, 0, out var command, out var argumentsStart))
        {
            return -1;
        }

        if (IsAuthLineSaslRulesCannotRead(command, line))
        {
            return argumentsStart;
        }

        if (Spells(command, "HELLO"u8))
        {
            return FindMaskStartAfterHelloAuth(line, argumentsStart);
        }

        return IsAuth(command) && IsOneSecretArgument(line[argumentsStart..]) ? argumentsStart : -1;
    }

    // Reads the first word at or after start that something other than white space follows; next is the
    // index just after the byte that ends the word.
    private static bool TryReadFollowedWord(
        ReadOnlySpan<byte> line, int start, out ReadOnlySpan<byte> word, out int next)
    {
        // Leading white space is not part of a word; a run of nothing else leaves it empty.
        var fromWord = line[start..].TrimStart(WhiteSpace);
        var length = fromWord.IndexOfAny(WhiteSpace);
        if (length < 0 || !fromWord[length..].ContainsAnyExcept(WhiteSpace))
        {
            word = default;
            next = -1;
            return false;
        }

        word = fromWord[..length];
        next = line.Length - fromWord.Length + length + 1;
        return true;
    }

    private static bool IsAuthLineSaslRulesCannotRead(ReadOnlySpan<byte> command, ReadOnlySpan<byte> line) =>
        line.ContainsAny(UnreadSeparators)
            ? IsAuth(command) || MayBeQuotedAuth(command)
            : MayBeQuotedAuth(command);

    // Just after the byte that ends the first AUTH among the words from start; -1 when none has arguments.
    private static int FindMaskStartAfterHelloAuth(ReadOnlySpan<byte> line, int start)
    {
        while (TryReadFollowedWord(line, start, out var word, out start))
        {
            if (Spells(word, "AUTH"u8) || MayBeEscaped(word))
            {
                return start;
            }
        }

        return -1;
    }

    // Whether the arguments are one only, and it is not the name of a mechanism.
    private static bool IsOneSecretArgument(ReadOnlySpan<byte> arguments)
    {
        var fromArgument = arguments.TrimStart(WhiteSpace);
        var length = ArgumentLength(fromArgument);

        return !fromArgument[length..].ContainsAnyExcept(WhiteSpace) && !IsMechanismName(fromArgument[..length]);
    }

    // The length of the argument that starts the text, with any white space it quotes.
    private static int ArgumentLength(ReadOnlySpan<byte> text)
    {
        var index = 0;
        while (index < text.Length && !WhiteSpace.Contains(text[index]))
        {
            index += Quotes.Contains(text[index]) ? QuotedLength(text[index..]) : 1;
        }

        return index;
    }

    // The length of the quoted run that starts the text; all of it when no quote closes it. As in Redis,
    // a backslash escapes any byte between double quotes and only a single quote between single quotes.
    private static int QuotedLength(ReadOnlySpan<byte> text)
    {
        for (var index = 1; index < text.Length; index++)
        {
            if (text[index] == Backslash && (text[0] == DoubleQuote || text[(index + 1)..].StartsWith("'"u8)))
            {
                index++;
            }
            else if (text[index] == text[0])
            {
                return index + 1;
            }
        }

        return text.Length;
    }

    private static bool IsMechanismName(ReadOnlySpan<byte> word)
    {
        foreach (var name in MechanismNames)
        {
            if (Ascii.EqualsIgnoreCase(word, name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAuth(ReadOnlySpan<byte> word) => Ascii.EqualsIgnoreCase(word, "AUTH"u8);

    private static bool MayBeQuotedAuth(ReadOnlySpan<byte> word) =>
        MayBeEscaped(word) || (word.ContainsAny(Quotes) && Spells(word, "AUTH"u8));

    private static bool MayBeEscaped(ReadOnlySpan<byte> word) =>
        word.Contains(DoubleQuote) && word.Contains(Backslash);

    // Whether the word is the name once its quotes, if it has any, are removed.
    private static bool Spells(ReadOnlySpan<byte> word, ReadOnlySpan<byte> name)
    {
        byte[] unquoted = [.. word.ToArray().Where(value => !Quotes.Contains(value))];

        return Ascii.EqualsIgnoreCase(unquoted, name);
    }
}
