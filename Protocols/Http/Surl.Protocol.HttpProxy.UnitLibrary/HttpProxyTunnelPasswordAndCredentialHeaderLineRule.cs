using System.Buffers;
using System.Text;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// The <see cref="ILineRedactionRule"/> for a password command or a credential header in a
/// <c>CONNECT</c> tunnel: it writes the argument of a <c>PASS</c> or <c>ACCT</c> command as
/// <see cref="LineBytesRedactor.SecretMask"/>, the credentials of a header named in
/// <see cref="HttpWithheldHeaderNames.Credentials"/> as <see cref="LineBytesRedactor.SecretMask"/>
/// after the scheme word (ADR-0182, amendment 16), and the whole value of a header named in
/// <see cref="HttpWithheldHeaderNames.ApiKeysAndSessionTokens"/> as
/// <see cref="LineBytesRedactor.SecretMask"/> (amendment 18). Commands and header names are matched
/// without regard to case. Every other line is written as received.
/// </summary>
/// <remarks>
/// A line that begins with a space or a tab and follows a header named in
/// <see cref="HttpWithheldHeaderNames.All"/>, or another such line, continues that header and is
/// masked.
/// </remarks>
internal sealed class HttpProxyTunnelPasswordAndCredentialHeaderLineRule : ILineRedactionRule
{
    private const int CommandPrefixLength = 5;

    private static ReadOnlySpan<byte> Blanks => " \t"u8;

    private bool previousLineHeldCredentials;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var continuesCredentials = previousLineHeldCredentials && line.Length > 0 && Blanks.Contains(line[0]);
        var secretStart = continuesCredentials ? SkipBlanks(line, 0) : FindSecretStart(line);
        previousLineHeldCredentials = continuesCredentials || IsHeaderNamedIn(line, HttpWithheldHeaderNames.All);
        if (secretStart >= line.Length)
        {
            output.Write(line);
            return;
        }

        output.Write(line[..secretStart]);
        output.Write(LineBytesRedactor.SecretMask);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        // The secret is always on the line that names it, whatever the origin sent.
    }

    // Where the secret begins in the line; the line's length when it holds none.
    private static int FindSecretStart(ReadOnlySpan<byte> line)
    {
        if (IsPasswordCommand(line))
        {
            return CommandPrefixLength;
        }

        if (IsHeaderNamedIn(line, HttpWithheldHeaderNames.ApiKeysAndSessionTokens))
        {
            return FindValueStart(line);
        }

        return IsHeaderNamedIn(line, HttpWithheldHeaderNames.Credentials) ? FindCredentialsStart(line) : line.Length;
    }

    private static bool IsPasswordCommand(ReadOnlySpan<byte> line) =>
        line.Length >= CommandPrefixLength
        && (Ascii.EqualsIgnoreCase(line[..CommandPrefixLength], "PASS "u8)
            || Ascii.EqualsIgnoreCase(line[..CommandPrefixLength], "ACCT "u8));

    private static bool IsHeaderNamedIn(ReadOnlySpan<byte> line, IReadOnlyList<string> names)
    {
        var colon = line.IndexOf((byte)':');

        return colon >= 0 && names.Contains(
            Encoding.Latin1.GetString(line[..colon].TrimEnd(Blanks)),
            StringComparer.OrdinalIgnoreCase);
    }

    // After the colon and any blanks; the line's length when the header has no value.
    private static int FindValueStart(ReadOnlySpan<byte> line) => SkipBlanks(line, line.IndexOf((byte)':') + 1);

    // After the colon and the scheme word; a value of one word is all credentials.
    private static int FindCredentialsStart(ReadOnlySpan<byte> line)
    {
        var valueStart = FindValueStart(line);
        var space = line[valueStart..].TrimEnd(Blanks).IndexOf((byte)' ');

        return space < 0 ? valueStart : SkipBlanks(line, valueStart + space);
    }

    private static int SkipBlanks(ReadOnlySpan<byte> line, int start)
    {
        var next = line[start..].IndexOfAnyExcept(Blanks);

        return next < 0 ? line.Length : start + next;
    }
}
