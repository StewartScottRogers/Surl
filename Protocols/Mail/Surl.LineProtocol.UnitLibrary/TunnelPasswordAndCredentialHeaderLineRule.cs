using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.LineProtocol;

/// <summary>
/// The <see cref="ILineRedactionRule"/> for a password command or a credential header in a tunnel: it
/// writes the argument of a <c>PASS</c> or <c>ACCT</c> command, its command word ended by a space or a
/// tab that is logged as received, as <see cref="LineBytesRedactor.SecretMask"/> (ADR-0182, amendment
/// 30), the credentials of a header named in
/// <see cref="HttpWithheldHeaderNames.Credentials"/> as <see cref="LineBytesRedactor.SecretMask"/>
/// after the scheme word, and the whole value of a header named in
/// <see cref="HttpWithheldHeaderNames.ApiKeysAndSessionTokens"/> as
/// <see cref="LineBytesRedactor.SecretMask"/> (ADR-0182, amendments 6, 16, 18, 19 and 20). Commands
/// and header names are matched without regard to case. Every other line is written as received.
/// </summary>
/// <remarks>
/// A line that begins with a space or a tab and follows a header named in
/// <see cref="HttpWithheldHeaderNames.All"/>, or another such line, continues that header and is
/// masked. One instance serves one tunnel.
/// </remarks>
public sealed class TunnelPasswordAndCredentialHeaderLineRule : ILineRedactionRule
{
    private const int CommandPrefixLength = 5;

    private static ReadOnlySpan<byte> Blanks => " \t"u8;

    private bool _previousLineHeldWithheldHeader;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var continuesWithheldHeader = _previousLineHeldWithheldHeader && line.Length > 0 && Blanks.Contains(line[0]);
        var secretStart = continuesWithheldHeader ? SkipBlanks(line, 0) : FindSecretStart(line);
        _previousLineHeldWithheldHeader = continuesWithheldHeader || IsHeaderNamedIn(line, HttpWithheldHeaderNames.All);
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

    // "PASS" or "ACCT" and the space or tab that ends the command word.
    private static bool IsPasswordCommand(ReadOnlySpan<byte> line) =>
        line.Length >= CommandPrefixLength
        && Blanks.Contains(line[CommandPrefixLength - 1])
        && (Ascii.EqualsIgnoreCase(line[..(CommandPrefixLength - 1)], "PASS"u8)
            || Ascii.EqualsIgnoreCase(line[..(CommandPrefixLength - 1)], "ACCT"u8));

    private static bool IsHeaderNamedIn(ReadOnlySpan<byte> line, IReadOnlyList<string> names)
    {
        int colon = line.IndexOf((byte)':');

        return colon >= 0 && names.Contains(
            Encoding.Latin1.GetString(line[..colon].TrimEnd(Blanks)),
            StringComparer.OrdinalIgnoreCase);
    }

    // After the colon and any blanks; the line's length when the header has no value.
    private static int FindValueStart(ReadOnlySpan<byte> line) => SkipBlanks(line, line.IndexOf((byte)':') + 1);

    // After the colon and the scheme word; a value of one word is all credentials.
    private static int FindCredentialsStart(ReadOnlySpan<byte> line)
    {
        int valueStart = FindValueStart(line);
        int space = line[valueStart..].TrimEnd(Blanks).IndexOf((byte)' ');

        return space < 0 ? valueStart : SkipBlanks(line, valueStart + space);
    }

    private static int SkipBlanks(ReadOnlySpan<byte> line, int start)
    {
        int next = line[start..].IndexOfAnyExcept(Blanks);

        return next < 0 ? line.Length : start + next;
    }
}
