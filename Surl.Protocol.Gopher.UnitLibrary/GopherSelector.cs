using System.Globalization;
using System.Text;

namespace Surl.Protocol.Gopher;

/// <summary>
/// Turns the selector a client sent into the percent-encoded request path the content store
/// maps, and a directory entry's name into the selector a menu offers for it.
/// </summary>
/// <remarks>
/// A selector is read as a percent-encoded path: every byte outside printable ASCII
/// (<c>0x21</c> to <c>0x7E</c>) is percent-encoded before the content store sees it, and a
/// <c>%</c> the client sent is left as it is, so the store decodes it. A menu encodes the
/// other way round: <c>%</c>, and every byte outside printable ASCII, is written
/// <c>%HH</c>, so each selector it offers maps back to exactly the entry it names.
/// </remarks>
internal static class GopherSelector
{
    private const byte Tab = (byte)'\t';

    /// <summary>
    /// Splits a selector line at its first TAB: the selector before it, and whether anything
    /// (a search string, or a Gopher+ <c>+</c>) came after it.
    /// </summary>
    /// <param name="line">The request line without its line ending.</param>
    /// <param name="selector">The bytes before the first TAB; the whole line when it has none.</param>
    /// <returns><see langword="true"/> when the line held a TAB.</returns>
    public static bool SplitAtTab(ReadOnlySpan<byte> line, out ReadOnlySpan<byte> selector)
    {
        var tab = line.IndexOf(Tab);
        selector = tab < 0 ? line : line[..tab];

        return tab >= 0;
    }

    /// <summary>
    /// Returns the request path the content store maps for <paramref name="selector"/>:
    /// the selector, percent-encoded outside printable ASCII, with a <c>/</c> put in front
    /// when it does not start with one. The empty selector is <c>/</c>, the root.
    /// </summary>
    /// <param name="selector">The selector bytes, without TAB or line ending.</param>
    /// <returns>A request path that starts with <c>/</c>.</returns>
    public static string ToRequestPath(ReadOnlySpan<byte> selector)
    {
        var path = new StringBuilder("/", selector.Length + 1);
        var start = selector.StartsWith("/"u8) ? 1 : 0;
        AppendEncoded(path, selector[start..], encodesPercent: false);

        return path.ToString();
    }

    /// <summary>
    /// Returns the selector a menu offers for the entry <paramref name="name"/> of the
    /// directory whose request path is <paramref name="directoryPath"/>.
    /// </summary>
    /// <param name="directoryPath">The directory's request path, as <see cref="ToRequestPath"/> returned it.</param>
    /// <param name="name">The entry's name, as the content store listed it.</param>
    /// <returns><paramref name="directoryPath"/> without its trailing <c>/</c>, a <c>/</c>, and the name percent-encoded.</returns>
    public static string ForEntry(string directoryPath, string name)
    {
        var selector = new StringBuilder(directoryPath.TrimEnd('/')).Append('/');
        AppendEncoded(selector, Encoding.UTF8.GetBytes(name), encodesPercent: true);

        return selector.ToString();
    }

    private static void AppendEncoded(StringBuilder text, ReadOnlySpan<byte> bytes, bool encodesPercent)
    {
        foreach (var value in bytes)
        {
            if (value is > 0x20 and < 0x7F && !(encodesPercent && value == (byte)'%'))
            {
                text.Append((char)value);
            }
            else
            {
                text.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }
        }
    }
}
