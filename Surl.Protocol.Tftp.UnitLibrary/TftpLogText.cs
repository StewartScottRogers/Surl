using System.Globalization;
using System.Text;

namespace Surl.Protocol.Tftp;

/// <summary>
/// Renders bytes a peer chose for the verbose log, as ADR-0006 section 3 requires.
/// </summary>
internal static class TftpLogText
{
    /// <summary>
    /// Renders printable ASCII (<c>0x20</c> to <c>0x7E</c>) other than backslash as itself,
    /// and every other byte as <c>\xHH</c>.
    /// </summary>
    /// <param name="bytes">The peer's bytes.</param>
    /// <returns>The rendering, reversible and free of control characters.</returns>
    public static string Render(ReadOnlySpan<byte> bytes)
    {
        var text = new StringBuilder(bytes.Length);
        foreach (var value in bytes)
        {
            if (value is >= 0x20 and < 0x7F and not (byte)'\\')
            {
                text.Append((char)value);
            }
            else
            {
                text.Append(@"\x").Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return text.ToString();
    }
}
