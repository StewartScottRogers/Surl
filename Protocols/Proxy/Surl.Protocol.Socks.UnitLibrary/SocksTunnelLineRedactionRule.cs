using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// The <see cref="ILineRedactionRule"/> for the lines of a text protocol carried through a SOCKS
/// tunnel, whichever protocol it is: it writes the argument of a <c>PASS</c> or <c>ACCT</c> command
/// as <see cref="LineBytesRedactor.SecretMask"/>, and the credentials of an <c>Authorization</c> or
/// <c>Proxy-Authorization</c> header as <see cref="LineBytesRedactor.SecretMask"/> after the scheme
/// word (ADR-0182, amendment 6). Commands and header names are matched without regard to case. Every
/// other line is written as received.
/// </summary>
/// <remarks>
/// A line that begins with a space or a tab and follows a credential header, or another such line,
/// continues that header and is masked. A line feed with no carriage return before it ends a line
/// too, so a client that sends bare line feeds has each of its lines judged.
/// </remarks>
internal sealed class SocksTunnelLineRedactionRule : ILineRedactionRule
{
    private const byte LineFeed = (byte)'\n';
    private const int CommandPrefixLength = 5;

    private bool previousLineHeldCredentials;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        int end;
        while ((end = line.IndexOf(LineFeed)) >= 0)
        {
            RedactOneLine(line[..end], output);
            output.Write([LineFeed]);
            line = line[(end + 1)..];
        }

        RedactOneLine(line, output);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        // The secret is always on the line that names it, whatever the origin sent.
    }

    private void RedactOneLine(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        bool continuesCredentials = previousLineHeldCredentials && line.Length > 0 && IsBlank(line[0]);
        int secretStart = continuesCredentials ? SkipBlanks(line, 0) : FindSecretStart(line);
        previousLineHeldCredentials = continuesCredentials || IsCredentialHeader(line);
        if (secretStart >= line.Length)
        {
            output.Write(line);
            return;
        }

        output.Write(line[..secretStart]);
        output.Write(LineBytesRedactor.SecretMask);
    }

    // Where the secret begins in the line; the line's length when it holds none.
    private static int FindSecretStart(ReadOnlySpan<byte> line)
    {
        if (IsPasswordCommand(line))
        {
            return CommandPrefixLength;
        }

        return IsCredentialHeader(line) ? FindCredentialsStart(line) : line.Length;
    }

    private static bool IsPasswordCommand(ReadOnlySpan<byte> line) =>
        StartsWithIgnoringCase(line, "PASS "u8) || StartsWithIgnoringCase(line, "ACCT "u8);

    private static bool IsCredentialHeader(ReadOnlySpan<byte> line)
    {
        int colon = line.IndexOf((byte)':');
        if (colon < 0)
        {
            return false;
        }

        ReadOnlySpan<byte> name = line[..colon].TrimEnd(" \t"u8);

        return IsIgnoringCase(name, "Authorization"u8) || IsIgnoringCase(name, "Proxy-Authorization"u8);
    }

    // After the colon and the scheme word; a value of one word is all credentials.
    private static int FindCredentialsStart(ReadOnlySpan<byte> line)
    {
        int valueStart = SkipBlanks(line, line.IndexOf((byte)':') + 1);
        int space = line[valueStart..].TrimEnd(" \t"u8).IndexOf((byte)' ');

        return space < 0 ? valueStart : SkipBlanks(line, valueStart + space);
    }

    private static int SkipBlanks(ReadOnlySpan<byte> line, int start)
    {
        while (start < line.Length && IsBlank(line[start]))
        {
            start++;
        }

        return start;
    }

    private static bool IsBlank(byte value) => value is (byte)' ' or (byte)'\t';

    private static bool StartsWithIgnoringCase(ReadOnlySpan<byte> line, ReadOnlySpan<byte> prefix) =>
        line.Length >= prefix.Length && IsIgnoringCase(line[..prefix.Length], prefix);

    private static bool IsIgnoringCase(ReadOnlySpan<byte> word, ReadOnlySpan<byte> expected) =>
        System.Text.Ascii.EqualsIgnoreCase(word, expected);
}
