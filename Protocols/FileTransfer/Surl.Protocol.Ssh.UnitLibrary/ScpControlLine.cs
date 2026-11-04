using System.Globalization;
using System.Text;

namespace Surl.Protocol.Ssh;

/// <summary>
/// SCP's control lines (ADR-0054, decisions 3 and 4): the <c>T</c> and <c>C</c> lines a source
/// sends, and the reading of those a sink receives.
/// </summary>
internal static class ScpControlLine
{
    /// <summary>The most octal a <c>C</c> line's mode may be, <c>07777</c>.</summary>
    public const int MaxMode = 0xFFF;

    private const int MaxModeDigits = 6;

    private const int MaxSizeDigits = 19;

    private static readonly long MaxUnixSeconds = DateTimeOffset.MaxValue.ToUnixTimeSeconds();

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// The <c>T</c> line a source sends for a file last written at <paramref name="lastWriteTime"/>:
    /// <c>T&lt;mtime&gt; 0 &lt;mtime&gt; 0</c> and LF, whole Unix seconds, the store keeping no access time.
    /// </summary>
    /// <param name="lastWriteTime">When the file was last written.</param>
    /// <returns>The line's bytes, LF included.</returns>
    public static byte[] Times(DateTimeOffset lastWriteTime)
    {
        var seconds = Math.Max(0, lastWriteTime.ToUnixTimeSeconds()).ToString(CultureInfo.InvariantCulture);

        return Encoding.ASCII.GetBytes($"T{seconds} 0 {seconds} 0\n");
    }

    /// <summary>
    /// The <c>C</c> line a source sends: <c>C0644 &lt;size&gt; &lt;name&gt;</c> and LF.
    /// </summary>
    /// <param name="size">The file's length.</param>
    /// <param name="name">The file's name.</param>
    /// <returns>The line's bytes, LF included.</returns>
    public static byte[] File(long size, string name) =>
        Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"C0644 {size} {name}\n"));

    /// <summary>
    /// Reads a <c>T&lt;mtime&gt; &lt;usec&gt; &lt;atime&gt; &lt;usec&gt;</c> line: four decimal
    /// fields separated by single spaces.
    /// </summary>
    /// <param name="line">The line, without its LF.</param>
    /// <param name="lastWriteTime">The <c>mtime</c>, when the line is one.</param>
    /// <returns>Whether the line is a well-formed <c>T</c> line.</returns>
    public static bool TryReadTimes(ReadOnlySpan<byte> line, out DateTimeOffset lastWriteTime)
    {
        var seconds = line.StartsWith("T"u8) ? ReadModificationSeconds(Encoding.Latin1.GetString(line[1..]).Split(' ')) : null;
        lastWriteTime = seconds is null ? default : DateTimeOffset.FromUnixTimeSeconds(seconds.Value);

        return seconds is not null;
    }

    /// <summary>
    /// Reads a <c>C&lt;mode&gt; &lt;size&gt; &lt;name&gt;</c> line.
    /// </summary>
    /// <param name="line">The line, without its LF.</param>
    /// <param name="header">The file's header, when the line is one.</param>
    /// <returns>
    /// Empty when the line is a well-formed <c>C</c> line; otherwise what is wrong with it:
    /// <c>received directory without -r</c> for a <c>D</c> or <c>E</c> line, <c>bad mode</c>,
    /// <c>bad size</c>, <c>unexpected filename</c>, or <c>unexpected line</c> for anything else.
    /// </returns>
    public static string ReadFile(ReadOnlySpan<byte> line, out ScpFileHeader? header)
    {
        header = null;
        var kind = line.IsEmpty ? (byte)0 : line[0];
        if (kind != 'C')
        {
            return kind is (byte)'D' or (byte)'E' ? "received directory without -r" : "unexpected line";
        }

        var mode = NextField(line[1..], out var rest);
        var size = ReadDecimal(Encoding.Latin1.GetString(NextField(rest, out var nameBytes)));
        var name = ReadName(nameBytes);
        var error = FileFieldError(IsMode(mode), size, name);
        if (error.Length == 0)
        {
            header = new ScpFileHeader(Encoding.ASCII.GetString(mode), size!.Value, name!);
        }

        return error;
    }

    // The T line's four fields, all decimal, the mtime within DateTimeOffset's range; its mtime,
    // or null for anything else.
    private static long? ReadModificationSeconds(string[] fields)
    {
        var values = fields.Select(ReadDecimal).ToArray();
        var wellFormed = values.Length == 4 && !values.Contains(null);

        return wellFormed && values[0] <= MaxUnixSeconds ? values[0] : null;
    }

    // What is wrong with a C line's fields, first wrong first; empty when nothing is.
    private static string FileFieldError(bool isMode, long? size, string? name)
    {
        if (!isMode)
        {
            return "bad mode";
        }

        return size is null ? "bad size" : name is null ? "unexpected filename" : string.Empty;
    }

    // The bytes before the next space, and those after it; all of them, and none, without one.
    private static ReadOnlySpan<byte> NextField(ReadOnlySpan<byte> bytes, out ReadOnlySpan<byte> rest)
    {
        var space = bytes.IndexOf((byte)' ');
        rest = space < 0 ? [] : bytes[(space + 1)..];

        return space < 0 ? bytes : bytes[..space];
    }

    // One to six octal digits, at most 07777.
    private static bool IsMode(ReadOnlySpan<byte> mode) =>
        mode.Length is > 0 and <= MaxModeDigits
            && !mode.ContainsAnyExceptInRange((byte)'0', (byte)'7')
            && Convert.ToInt32(Encoding.ASCII.GetString(mode), 8) <= MaxMode;

    // One path segment of UTF-8: not empty, not . or .., no /; null for anything else.
    private static string? ReadName(ReadOnlySpan<byte> name)
    {
        string text;
        try
        {
            text = StrictUtf8.GetString(name);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }

        return IsSegment(text) ? text : null;
    }

    private static bool IsSegment(string text) =>
        text is not ("" or "." or "..") && !text.Contains('/', StringComparison.Ordinal);

    // One to 19 decimal digits within a long; null for anything else.
    private static long? ReadDecimal(string field) =>
        field.Length is > 0 and <= MaxSizeDigits && field.All(char.IsAsciiDigit)
            && long.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
