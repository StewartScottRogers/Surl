using System.Text;

namespace Surl.Protocol.Imap;

/// <summary>
/// Mailbox names on the wire (ADR-0055, decision 6): read from modified UTF-7 (RFC 3501,
/// section 5.1.3) with bytes of 0x80 and above read as UTF-8, since curl sends a URL's UTF-8
/// bytes as they are; written back in modified UTF-7, as an atom when they are one and as a
/// quoted string otherwise. The hierarchy delimiter is <c>/</c>.
/// </summary>
internal static class ImapMailboxName
{
    /// <summary>
    /// The hierarchy delimiter.
    /// </summary>
    public const char Delimiter = '/';

    private const string Inbox = "INBOX";

    private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly Encoding StrictUtf16BigEndian = new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Reads a name, or a <c>LIST</c> reference or pattern, as the client sent it.
    /// </summary>
    /// <param name="bytes">The name's bytes: an atom's, a quoted string's content or a literal.</param>
    /// <returns>The name, with <c>INBOX</c> in any case as <c>INBOX</c>; <see langword="null"/>
    /// when it holds a control character, a bad <c>&amp;...-</c> run or bytes that are not UTF-8.</returns>
    public static string? Decode(byte[] bytes)
    {
        var name = new StringBuilder(bytes.Length);
        var index = 0;
        while (index < bytes.Length)
        {
            if (DecodeNext(bytes, ref index) is not { } next)
            {
                return null;
            }

            name.Append(next);
            index++;
        }

        return string.Equals(name.ToString(), Inbox, StringComparison.OrdinalIgnoreCase) ? Inbox : name.ToString();
    }

    // The characters the bytes from index on begin with; the index is left on their last byte.
    private static string? DecodeNext(byte[] bytes, ref int index) => bytes[index] switch
    {
        (byte)'&' => DecodeShifted(bytes, ref index),
        >= 0x80 => DecodeUtf8Run(bytes, ref index),
        < 0x20 or 0x7F => null,
        var ascii => ((char)ascii).ToString(),
    };

    /// <summary>
    /// Whether <paramref name="name"/> may name a mailbox: no empty level (<c>a//b</c>, a leading
    /// or trailing <c>/</c>) and no <c>*</c> or <c>%</c>. The store refuses the rest itself.
    /// </summary>
    /// <param name="name">A decoded name.</param>
    /// <returns>Whether it is a mailbox name.</returns>
    public static bool IsMailboxName(string name) =>
        name.Split(Delimiter).All(level => level.Length > 0) && name.AsSpan().IndexOfAny('*', '%') < 0;

    /// <summary>
    /// Writes <paramref name="name"/> for a response: in modified UTF-7, as an atom when every
    /// character is an <c>ASTRING-CHAR</c>, else as a quoted string with <c>\</c> before each
    /// <c>"</c> and <c>\</c>.
    /// </summary>
    /// <param name="name">The name as the store keeps it.</param>
    /// <returns>Printable ASCII.</returns>
    public static string ToWire(string name)
    {
        var encoded = EncodeModifiedUtf7(name);
        return encoded.Length > 0 && !encoded.AsSpan().ContainsAny("(){ %*\"\\")
            ? encoded
            : "\"" + encoded.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    // "&-" is "&"; "&<modified base64>-" is UTF-16BE (RFC 3501, section 5.1.3). The index is
    // left on the closing "-".
    private static string? DecodeShifted(byte[] bytes, ref int index)
    {
        var end = Array.IndexOf(bytes, (byte)'-', index + 1);
        if (end < 0)
        {
            return null;
        }

        var start = index + 1;
        index = end;
        return end == start ? "&" : DecodeModifiedBase64(Encoding.ASCII.GetString(bytes, start, end - start));
    }

    private static string? DecodeModifiedBase64(string base64)
    {
        var padded = base64.Replace(',', '/') + new string('=', (4 - (base64.Length % 4)) % 4);
        var utf16 = new byte[padded.Length];
        try
        {
            return base64.Contains('/') || !Convert.TryFromBase64String(padded, utf16, out var length)
                ? null
                : StrictUtf16BigEndian.GetString(utf16, 0, length);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    // A run of bytes of 0x80 and above, read as UTF-8. The index is left on the run's last byte.
    private static string? DecodeUtf8Run(byte[] bytes, ref int index)
    {
        var start = index;
        while (index + 1 < bytes.Length && bytes[index + 1] >= 0x80)
        {
            index++;
        }

        try
        {
            return StrictUtf8.GetString(bytes, start, index - start + 1);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static string EncodeModifiedUtf7(string name)
    {
        var encoded = new StringBuilder(name.Length);
        var index = 0;
        while (index < name.Length)
        {
            var run = ShiftedRunLength(name, index);
            encoded.Append(run > 0 ? EncodeShifted(name.Substring(index, run)) : name[index] == '&' ? "&-" : name[index].ToString());
            index += Math.Max(run, 1);
        }

        return encoded.ToString();
    }

    // How many characters from start on are outside printable ASCII, and so written shifted.
    private static int ShiftedRunLength(string name, int start)
    {
        var end = start;
        while (end < name.Length && name[end] is < ' ' or > '~')
        {
            end++;
        }

        return end - start;
    }

    private static string EncodeShifted(string run) =>
        "&" + Convert.ToBase64String(StrictUtf16BigEndian.GetBytes(run)).TrimEnd('=').Replace('/', ',') + "-";
}
