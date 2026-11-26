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
/// <para>
/// A line is a <c>LOGIN</c> command when its first or second word is <c>LOGIN</c>, matched without
/// regard to case; words are separated by runs of spaces and tabs. The server refuses a line with no
/// tag, one that starts with a space and one that separates words with a tab, but the client has
/// already sent the password, so it is withheld all the same.
/// </para>
/// <para>
/// When the first two words are both <c>LOGIN</c>, the first may be the tag or the second may be the
/// user name of a line with no tag, so both arguments after the second are withheld.
/// </para>
/// <para>
/// When a tab is on the line that holds the password, a tab may have split one argument in two, so
/// nothing after the mask is written, and a literal that line announces is withheld too.
/// </para>
/// </remarks>
internal sealed class ImapLineRedactionRule : ILineRedactionRule
{
    private const byte Tab = (byte)'\t';
    private const byte Quote = (byte)'"';
    private const byte Backslash = (byte)'\\';
    private const int NotInLogin = -1;
    private const int PasswordArgumentIndex = 1;
    private const int LineEndLength = 2;

    private static ReadOnlySpan<byte> LoginWord => "LOGIN"u8;

    private static ReadOnlySpan<byte> WordSeparators => " \t"u8;

    private readonly SaslLineRedactionRule _saslRule = new(["AUTHENTICATE"], "+ ");
    private readonly ArrayBufferWriter<byte> _saslOutput = new();
    private bool _inLiteral;
    private bool _awaitingLiteralContinuation;
    private long _literalRemaining;
    private int _loginArgumentIndex = NotInLogin;
    private bool _firstArgumentMayBePassword;

    private bool IsAtPossiblePassword =>
        _loginArgumentIndex == PasswordArgumentIndex || (_loginArgumentIndex == 0 && _firstArgumentMayBePassword);

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
        _loginArgumentIndex = NotInLogin;
        if (TryFindLoginArguments(line, out var argumentsStart, out _firstArgumentMayBePassword))
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
        var holdsTab = text.Contains(Tab);
        while (true)
        {
            var separatorCount = text.Length - text.TrimStart(WordSeparators).Length;
            output.Write(text[..separatorCount]);
            text = text[separatorCount..];
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
            output.Write(IsAtPossiblePassword ? LineBytesRedactor.SecretMask : argument);
            if (_loginArgumentIndex == PasswordArgumentIndex)
            {
                RedactAfterPassword(text, holdsTab, output);
                return;
            }

            _loginArgumentIndex++;
        }
    }

    private void RedactAfterPassword(ReadOnlySpan<byte> text, bool holdsTab, ArrayBufferWriter<byte> output)
    {
        // With a tab the argument index stays at the password, so a literal announced here is masked.
        if (!holdsTab)
        {
            _loginArgumentIndex = NotInLogin;
            output.Write(text);
        }

        AnnounceTrailingLiteral(text);
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
        output.Write(IsAtPossiblePassword ? LineBytesRedactor.SecretMask : bytes);
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

    private static bool TryFindLoginArguments(
        ReadOnlySpan<byte> line, out int argumentsStart, out bool firstArgumentMayBePassword)
    {
        var fromFirstWord = line.TrimStart(WordSeparators);
        var fromSecondWord = fromFirstWord[AtomLength(fromFirstWord)..].TrimStart(WordSeparators);
        var isFirstWordLogin = StartsWithLoginWord(fromFirstWord);
        var isSecondWordLogin = StartsWithLoginWord(fromSecondWord);
        var fromLoginWord = isSecondWordLogin ? fromSecondWord : fromFirstWord;
        argumentsStart = line.Length - fromLoginWord.Length + LoginWord.Length;
        firstArgumentMayBePassword = isFirstWordLogin && isSecondWordLogin;
        return isFirstWordLogin || isSecondWordLogin;
    }

    private static bool StartsWithLoginWord(ReadOnlySpan<byte> text) =>
        Ascii.EqualsIgnoreCase(text[..AtomLength(text)], LoginWord);

    private static int ArgumentLength(ReadOnlySpan<byte> text) =>
        text[0] == Quote ? QuotedLength(text) : AtomLength(text);

    private static int AtomLength(ReadOnlySpan<byte> text)
    {
        var separatorAt = text.IndexOfAny(WordSeparators);
        return separatorAt < 0 ? text.Length : separatorAt;
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
