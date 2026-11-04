using System.Globalization;
using System.Text;

namespace Surl.Protocol.Tftp;

/// <summary>
/// Turns the file name a client sent into the percent-encoded request path the content
/// store maps.
/// </summary>
internal static class TftpFileName
{
    /// <summary>
    /// Returns the request path for <paramref name="fileName"/>: a <c>/</c>, then the name
    /// without one leading <c>/</c>, with <c>%</c> and every byte outside printable ASCII
    /// (<c>0x21</c> to <c>0x7E</c>) percent-encoded, so the store decodes exactly the bytes
    /// sent. Upstream curl sends the URL's path percent-decoded and without its first
    /// <c>/</c>, so <c>tftp://host/sub/a%20b.txt</c> sends <c>sub/a b.txt</c>, which maps
    /// to <c>/sub/a%20b.txt</c>.
    /// </summary>
    /// <param name="fileName">The file name's bytes, as sent.</param>
    /// <returns>A request path that starts with <c>/</c>.</returns>
    public static string ToRequestPath(ReadOnlySpan<byte> fileName)
    {
        var path = new StringBuilder("/", fileName.Length + 1);
        var start = fileName.StartsWith("/"u8) ? 1 : 0;
        foreach (var value in fileName[start..])
        {
            if (value is > 0x20 and < 0x7F and not (byte)'%')
            {
                path.Append((char)value);
            }
            else
            {
                path.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return path.ToString();
    }
}
