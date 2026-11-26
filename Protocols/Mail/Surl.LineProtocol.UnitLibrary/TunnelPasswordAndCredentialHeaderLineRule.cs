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
/// <see cref="LineBytesRedactor.SecretMask"/> (ADR-0182, amendments 6, 16, 18, 19, 20 and 33). Commands
/// and header names are matched without regard to case. Every other line is written as received.
/// </summary>
/// <remarks>
/// <para>
/// A header is read at the start of the line and after every bare carriage return in it, with or
/// without spaces or tabs before its name. The first withheld header so read is masked as above, and
/// everything after the place its secret begins is dropped with it, later carriage returns included.
/// A command is read at the start of the line only.
/// </para>
/// <para>
/// A line that begins with a space or a tab and follows a line holding a header named in
/// <see cref="HttpWithheldHeaderNames.All"/>, or another such line, continues that header and is
/// masked. One instance serves one tunnel.
/// </para>
/// </remarks>
public sealed class TunnelPasswordAndCredentialHeaderLineRule : ILineRedactionRule
{
    private const int CommandPrefixLength = 5;
    private const byte CarriageReturn = (byte)'\r';

    private static ReadOnlySpan<byte> Blanks => " \t"u8;

    private bool _previousLineHeldWithheldHeader;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var continuesWithheldHeader = _previousLineHeldWithheldHeader && line.Length > 0 && Blanks.Contains(line[0]);
        var headerStart = FindWithheldHeaderStart(line);
        var secretStart = continuesWithheldHeader ? SkipBlanks(line, 0) : FindSecretStart(line, headerStart);
        _previousLineHeldWithheldHeader = continuesWithheldHeader || headerStart >= 0;
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
    private static int FindSecretStart(ReadOnlySpan<byte> line, int headerStart)
    {
        if (IsPasswordCommand(line))
        {
            return CommandPrefixLength;
        }

        return headerStart < 0 ? line.Length : headerStart + FindHeaderSecretStart(line[headerStart..]);
    }

    // Where the first withheld header begins, at the start of the line or after a bare carriage
    // return; -1 when the line holds none.
    private static int FindWithheldHeaderStart(ReadOnlySpan<byte> line)
    {
        foreach (var segment in line.Split(CarriageReturn))
        {
            if (IsHeaderNamedIn(line[segment], HttpWithheldHeaderNames.All))
            {
                return segment.Start.Value;
            }
        }

        return -1;
    }

    // Where the secret of the withheld header that the bytes begin with starts in them.
    private static int FindHeaderSecretStart(ReadOnlySpan<byte> fromHeader) =>
        IsHeaderNamedIn(UpToCarriageReturn(fromHeader), HttpWithheldHeaderNames.ApiKeysAndSessionTokens)
            ? FindValueStart(fromHeader)
            : FindCredentialsStart(fromHeader);

    private static ReadOnlySpan<byte> UpToCarriageReturn(ReadOnlySpan<byte> bytes)
    {
        int carriageReturn = bytes.IndexOf(CarriageReturn);

        return carriageReturn < 0 ? bytes : bytes[..carriageReturn];
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
            Encoding.Latin1.GetString(line[..colon].Trim(Blanks)),
            StringComparer.OrdinalIgnoreCase);
    }

    // After the colon and any blanks; the line's length when the header has no value.
    private static int FindValueStart(ReadOnlySpan<byte> line) => SkipBlanks(line, line.IndexOf((byte)':') + 1);

    // After the colon and the scheme word; a value of one word is all credentials. A scheme word
    // is kept only when its credentials follow it before any bare carriage return.
    private static int FindCredentialsStart(ReadOnlySpan<byte> line)
    {
        int valueStart = FindValueStart(line);
        int space = UpToCarriageReturn(line)[valueStart..].TrimEnd(Blanks).IndexOf((byte)' ');

        return space < 0 ? valueStart : SkipBlanks(line, valueStart + space);
    }

    private static int SkipBlanks(ReadOnlySpan<byte> line, int start)
    {
        int next = line[start..].IndexOfAnyExcept(Blanks);

        return next < 0 ? line.Length : start + next;
    }
}
