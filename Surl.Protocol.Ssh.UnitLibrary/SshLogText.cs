using System.Globalization;
using System.Text;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Renders bytes a peer chose for the verbose log, as ADR-0006 section 3 requires.
/// </summary>
internal static class SshLogText
{
    /// <summary>
    /// Renders printable ASCII (<c>0x20</c> to <c>0x7E</c>) other than backslash as itself,
    /// CR and LF as <c>\r</c> and <c>\n</c>, and every other byte as <c>\xHH</c>.
    /// </summary>
    /// <param name="bytes">The peer's bytes.</param>
    /// <returns>The rendering, reversible and free of control characters.</returns>
    public static string Render(ReadOnlySpan<byte> bytes)
    {
        var text = new StringBuilder(bytes.Length);
        foreach (var value in bytes)
        {
            text.Append(RenderByte(value));
        }

        return text.ToString();
    }

    /// <summary>
    /// Renders a name-list the peer sent, its names joined by commas.
    /// </summary>
    /// <param name="names">The names, each read one byte per character (Latin-1).</param>
    /// <returns>The rendering.</returns>
    public static string RenderNameList(IReadOnlyList<string> names) =>
        Render(Encoding.Latin1.GetBytes(string.Join(',', names)));

    private static string RenderByte(byte value) => value switch
    {
        (byte)'\r' => @"\r",
        (byte)'\n' => @"\n",
        (byte)'\\' => @"\x5C",
        >= 0x20 and < 0x7F => ((char)value).ToString(),
        _ => @"\x" + value.ToString("X2", CultureInfo.InvariantCulture),
    };
}
