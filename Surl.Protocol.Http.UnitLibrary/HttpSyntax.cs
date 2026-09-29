namespace Surl.Protocol.Http;

/// <summary>
/// The byte classes of RFC 9110, section 5.6, and RFC 9112 that a request head is checked
/// against.
/// </summary>
internal static class HttpSyntax
{
    private static readonly System.Buffers.SearchValues<byte> TokenBytes = System.Buffers.SearchValues.Create(
        "!#$%&'*+-.^_`|~0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"u8);

    /// <summary>
    /// Whether <paramref name="bytes"/> is a token: one or more tchar (RFC 9110, section 5.6.2).
    /// </summary>
    public static bool IsToken(ReadOnlySpan<byte> bytes) => !bytes.IsEmpty && !bytes.ContainsAnyExcept(TokenBytes);

    /// <summary>
    /// Whether <paramref name="bytes"/> can be a request target: one or more bytes, none of
    /// them whitespace, a control character or DEL.
    /// </summary>
    public static bool IsRequestTarget(ReadOnlySpan<byte> bytes) => !bytes.IsEmpty && !bytes.ContainsAnyInRange((byte)0x00, (byte)0x20) && !bytes.Contains((byte)0x7F);

    /// <summary>
    /// Whether <paramref name="bytes"/> holds only field-value bytes: VCHAR, obs-text, space
    /// and horizontal tab (RFC 9110, section 5.5).
    /// </summary>
    public static bool IsFieldValue(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            if (value is (< 0x20 and not (byte)'\t') or 0x7F)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether <paramref name="value"/> is optional whitespace: a space or a horizontal tab.
    /// </summary>
    public static bool IsOptionalWhitespace(byte value) => value is (byte)' ' or (byte)'\t';

    /// <summary>
    /// Returns <paramref name="bytes"/> without the optional whitespace at either end.
    /// </summary>
    public static ReadOnlySpan<byte> TrimOptionalWhitespace(ReadOnlySpan<byte> bytes) => bytes.Trim(" \t"u8);

    /// <summary>
    /// Returns <paramref name="line"/>, a line without its LF, without the CR that ends it,
    /// if one does (RFC 9112, section 2.2).
    /// </summary>
    public static ReadOnlySpan<byte> WithoutTrailingCarriageReturn(ReadOnlySpan<byte> line) =>
        line.EndsWith((byte)'\r') ? line[..^1] : line;
}
