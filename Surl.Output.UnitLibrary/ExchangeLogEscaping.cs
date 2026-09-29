using System.Text;

namespace Surl.Output;

/// <summary>
/// The escaped rendering of bytes in the verbose log (ADR-0006, section 3; ADR-0007,
/// section 8): each byte 0x20 to 0x7E except backslash as itself, CR as <c>\r</c>, LF as
/// <c>\n</c>, and every other byte, backslash included, as <c>\x</c> and two upper-case
/// hex digits. The rendering is printable ASCII and reversible.
/// </summary>
public static class ExchangeLogEscaping
{
    /// <summary>
    /// Appends the escaped rendering of <paramref name="bytes"/> to <paramref name="builder"/>.
    /// </summary>
    /// <param name="bytes">The bytes to render.</param>
    /// <param name="builder">Where the rendering goes.</param>
    public static void AppendEscaped(ReadOnlySpan<byte> bytes, StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        foreach (var value in bytes)
        {
            AppendEscaped(value, builder);
        }
    }

    /// <summary>
    /// Returns the escaped rendering of <paramref name="bytes"/>.
    /// </summary>
    /// <param name="bytes">The bytes to render.</param>
    /// <returns>The rendering.</returns>
    public static string Escape(ReadOnlySpan<byte> bytes)
    {
        var builder = new StringBuilder(bytes.Length);
        AppendEscaped(bytes, builder);
        return builder.ToString();
    }

    private static void AppendEscaped(byte value, StringBuilder builder)
    {
        switch (value)
        {
            case (byte)'\r':
                builder.Append("\\r");
                break;
            case (byte)'\n':
                builder.Append("\\n");
                break;
            case >= 0x20 and <= 0x7E when value != (byte)'\\':
                builder.Append((char)value);
                break;
            default:
                builder.Append("\\x").Append(value.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
                break;
        }
    }
}
