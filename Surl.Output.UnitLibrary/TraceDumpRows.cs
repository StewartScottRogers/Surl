using System.Globalization;
using System.Text;

namespace Surl.Output;

/// <summary>
/// The rows of one trace dump event, as upstream curl 8.21.0 writes them (ADR-0033,
/// section 4): each row the offset of its first byte as at least four lower-case hex
/// digits and <c>: </c>, then, for <see cref="TraceDumpLayout.HexAndAscii"/>, sixteen bytes
/// as <c>hh </c> (three spaces for each byte past the end) and those bytes as characters,
/// or, for <see cref="TraceDumpLayout.Ascii"/>, at most 64 bytes as characters, the row
/// ending early after a CR LF pair, which is not printed. A byte 0x20 to 0x7E is its own
/// character and every other byte is <c>.</c>. Offsets start at <c>0000</c> in each event.
/// </summary>
internal static class TraceDumpRows
{
    /// <summary>
    /// The bytes one <see cref="TraceDumpLayout.HexAndAscii"/> row holds.
    /// </summary>
    internal const int HexBytesPerRow = 16;

    /// <summary>
    /// The most bytes one <see cref="TraceDumpLayout.Ascii"/> row prints.
    /// </summary>
    internal const int AsciiBytesPerRow = 64;

    /// <summary>
    /// Appends the rows of <paramref name="bytes"/>, each ending with <see cref="Environment.NewLine"/>.
    /// </summary>
    /// <param name="bytes">The event's bytes.</param>
    /// <param name="layout">The layout.</param>
    /// <param name="builder">Where the rows go.</param>
    public static void Append(ReadOnlySpan<byte> bytes, TraceDumpLayout layout, StringBuilder builder)
    {
        if (layout == TraceDumpLayout.HexAndAscii)
        {
            AppendHexAndAsciiRows(bytes, builder);
        }
        else
        {
            AppendAsciiRows(bytes, builder);
        }
    }

    private static void AppendHexAndAsciiRows(ReadOnlySpan<byte> bytes, StringBuilder builder)
    {
        for (var offset = 0; offset < bytes.Length; offset += HexBytesPerRow)
        {
            var row = bytes.Slice(offset, Math.Min(HexBytesPerRow, bytes.Length - offset));
            AppendOffset(offset, builder);
            foreach (var value in row)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture)).Append(' ');
            }

            builder.Append(' ', (HexBytesPerRow - row.Length) * 3);
            AppendCharacters(row, builder);
            builder.Append(Environment.NewLine);
        }
    }

    private static void AppendAsciiRows(ReadOnlySpan<byte> bytes, StringBuilder builder)
    {
        var offset = 0;
        while (offset < bytes.Length)
        {
            var (printed, skipped) = AsciiRowLength(bytes[offset..]);
            AppendOffset(offset, builder);
            AppendCharacters(bytes.Slice(offset, printed), builder);
            builder.Append(Environment.NewLine);
            offset += printed + skipped;
        }
    }

    /// <summary>
    /// How many bytes the next <see cref="TraceDumpLayout.Ascii"/> row prints, and how many
    /// after them it consumes unprinted: a CR LF pair that starts within the row or right
    /// after a full row ends the row and is skipped, as curl's <c>dump()</c> skips it.
    /// </summary>
    private static (int Printed, int Skipped) AsciiRowLength(ReadOnlySpan<byte> rest)
    {
        var window = rest[..Math.Min(rest.Length, AsciiBytesPerRow + 2)];
        var lineEnd = window.IndexOf("\r\n"u8);
        return lineEnd < 0 ? (Math.Min(rest.Length, AsciiBytesPerRow), 0) : (lineEnd, 2);
    }

    private static void AppendOffset(int offset, StringBuilder builder) =>
        builder.Append(offset.ToString("x4", CultureInfo.InvariantCulture)).Append(": ");

    private static void AppendCharacters(ReadOnlySpan<byte> bytes, StringBuilder builder)
    {
        foreach (var value in bytes)
        {
            builder.Append(value is >= 0x20 and <= 0x7E ? (char)value : '.');
        }
    }
}
