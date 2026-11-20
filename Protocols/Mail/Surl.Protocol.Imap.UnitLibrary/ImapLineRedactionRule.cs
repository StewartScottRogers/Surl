using System.Buffers;
using System.Text;
using Surl.LineProtocol;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Imap;

/// <summary>
/// The <see cref="ILineRedactionRule"/> of the IMAP connection: it writes the password argument of
/// <c>LOGIN</c> as <see cref="LineBytesRedactor.SecretMask"/> whether the client sends it as an atom,
/// a quoted string or a literal, withholds every <c>AUTHENTICATE</c> secret through a
/// <see cref="SaslLineRedactionRule"/>, and logs every other line, an <c>APPEND</c> message's literal
/// bytes included, unchanged (ADR-0183, decision 5 as BL-1430 settled it).
/// </summary>
/// <remarks>
/// <para>
/// One instance serves one exchange. A command line that ends in a literal marker (<c>{6}</c>, or
/// <c>{6+}</c>) starts a literal: the next 6 bytes the client sends, across as many CR LF lines as
/// they span, are the literal's bytes, and they never reach the SASL rule, so a message that holds
/// the text <c>LOGIN a b</c> is logged as it is. The server's <c>+</c> that invites a synchronizing
/// literal is not a SASL continuation for the same reason; any other first line the server sends
/// after a synchronizing marker refuses the literal, and the rule goes back to reading commands.
/// </para>
/// <para>
/// The bytes after a literal, on the line that finishes it, are the rest of the same command.
/// </para>
/// </remarks>
internal sealed class ImapLineRedactionRule : ILineRedactionRule
{
    private const byte Space = (byte)' ';
    private const byte Quote = (byte)'"';
    private const byte Backslash = (byte)'\\';
    private const int NotInLogin = -1;
    private const int PasswordArgumentIndex = 1;
    private const int LineEndLength = 2;

    private readonly SaslLineRedactionRule _saslRule = new(["AUTHENTICATE"], "+ ");
    private readonly ArrayBufferWriter<byte> _saslOutput = new();
    private bool _inLiteral;
    private bool _awaitingLiteralContinuation;
    private long _literalRemaining;
    private int _loginArgumentIndex = NotInLogin;

    /// <inheritdoc/>
    public void Redact(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (_inLiteral)
        {
            RedactLiteralLine(line, output);
        }
        else if (!WasMaskedBySasl(line, output))
        {
            RedactCommandLine(line, output);
        }
    }

    /// <inheritdoc/>
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        if (_awaitingLiteralContinuation)
        {
            _awaitingLiteralContinuation = false;
            if (!sent.StartsWith("+"u8))
            {
                CancelLiteral();
            }
        }
        else if (!_inLiteral)
        {
            _saslRule.ObserveSent(sent);
        }
    }

    private bool WasMaskedBySasl(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        _saslOutput.Clear();
        _saslRule.Redact(line, _saslOutput);
        if (_saslOutput.WrittenSpan.SequenceEqual(line))
        {
            return false;
        }

        output.Write(_saslOutput.WrittenSpan);
        return true;
    }

    private void RedactCommandLine(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        if (TryFindLoginArguments(line, out var argumentsStart))
        {
            output.Write(line[..argumentsStart]);
            _loginArgumentIndex = 0;
            RedactCommandText(line[argumentsStart..], output);
        }
        else
        {
            RedactCommandText(line, output);
        }
    }

    private void RedactCommandText(ReadOnlySpan<byte> text, ArrayBufferWriter<byte> output)
    {
        if (_loginArgumentIndex == NotInLogin)
        {
            output.Write(text);
            AnnounceTrailingLiteral(text);
        }
        else
        {
            RedactLoginArguments(text, output);
        }
    }

    private void RedactLoginArguments(ReadOnlySpan<byte> text, ArrayBufferWriter<byte> output)
    {
        while (true)
        {
            var spaceCount = text.Length - text.TrimStart(Space).Length;
            output.Write(text[..spaceCount]);
            text = text[spaceCount..];
            if (text.IsEmpty)
            {
                return;
            }

            if (ImapLiteralMarker.Find(text.ToArray()) is { Start: 0 } marker)
            {
                output.Write(text);
                AnnounceLiteral(marker);
                return;
            }

            var argument = text[..ArgumentLength(text)];
            text = text[argument.Length..];
            if (_loginArgumentIndex == PasswordArgumentIndex)
            {
                output.Write(LineBytesRedactor.SecretMask);
                _loginArgumentIndex = NotInLogin;
                output.Write(text);
                AnnounceTrailingLiteral(text);
                return;
            }

            output.Write(argument);
            _loginArgumentIndex++;
        }
    }

    private void RedactLiteralLine(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        var lineWithEnd = line.Length + (long)LineEndLength;
        if (_literalRemaining >= lineWithEnd)
        {
            _literalRemaining -= lineWithEnd;
            WriteLiteralBytes(line, output);
            return;
        }

        var literalLength = (int)Math.Min(_literalRemaining, line.Length);
        WriteLiteralBytes(line[..literalLength], output);
        FinishLiteral();
        RedactCommandText(line[literalLength..], output);
    }

    private void WriteLiteralBytes(ReadOnlySpan<byte> bytes, ArrayBufferWriter<byte> output)
    {
        output.Write(_loginArgumentIndex == PasswordArgumentIndex ? LineBytesRedactor.SecretMask : bytes);
    }

    private void FinishLiteral()
    {
        _inLiteral = false;
        if (_loginArgumentIndex == PasswordArgumentIndex)
        {
            _loginArgumentIndex = NotInLogin;
        }
        else if (_loginArgumentIndex != NotInLogin)
        {
            _loginArgumentIndex++;
        }
    }

    private void CancelLiteral()
    {
        _inLiteral = false;
        _loginArgumentIndex = NotInLogin;
    }

    private void AnnounceTrailingLiteral(ReadOnlySpan<byte> text)
    {
        if (ImapLiteralMarker.Find(text.ToArray()) is { } marker)
        {
            AnnounceLiteral(marker);
        }
    }

    private void AnnounceLiteral(ImapLiteralMarker marker)
    {
        _inLiteral = true;
        _literalRemaining = marker.Length;
        _awaitingLiteralContinuation = !marker.IsNonSynchronizing;
    }

    private static bool TryFindLoginArguments(ReadOnlySpan<byte> line, out int argumentsStart)
    {
        argumentsStart = 0;
        var tagEnd = line.IndexOf(Space);
        if (tagEnd < 0)
        {
            return false;
        }

        var afterTag = line[(tagEnd + 1)..];
        var wordLength = afterTag.IndexOf(Space);
        var word = wordLength < 0 ? afterTag : afterTag[..wordLength];
        argumentsStart = tagEnd + 1 + word.Length;
        return Ascii.EqualsIgnoreCase(word, "LOGIN"u8);
    }

    private static int ArgumentLength(ReadOnlySpan<byte> text) =>
        text[0] == Quote ? QuotedLength(text) : AtomLength(text);

    private static int AtomLength(ReadOnlySpan<byte> text)
    {
        var spaceAt = text.IndexOf(Space);
        return spaceAt < 0 ? text.Length : spaceAt;
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
}
