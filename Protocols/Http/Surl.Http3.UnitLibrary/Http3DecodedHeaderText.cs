using System.Text;
using Surl.Qpack;

namespace Surl.Http3;

/// <summary>
/// The text a decoded header list is published as (ADR-0169, decision 1): one <c>name: value</c> line per
/// header, each ending CRLF, in the order given, then a blank line. A CR or LF inside a name or value, which
/// RFC 9114 makes malformed, is written as the two characters <c>\r</c> or <c>\n</c>, so a header never runs
/// onto a second line, where its value would escape redaction (ADR-0182, amendment 22). A backslash is
/// written as it is.
/// </summary>
internal static class Http3DecodedHeaderText
{
    /// <summary>Renders <paramref name="fieldLines"/> as the published text, in UTF-8.</summary>
    /// <param name="fieldLines">The header list, in the order received or sent.</param>
    /// <returns>The text's bytes.</returns>
    public static byte[] Bytes(IEnumerable<QpackFieldLine> fieldLines)
    {
        var text = new StringBuilder();
        foreach (var line in fieldLines)
        {
            text.Append(OnOneLine(line.Name)).Append(": ").Append(OnOneLine(line.Value)).Append("\r\n");
        }

        return Encoding.UTF8.GetBytes(text.Append("\r\n").ToString());
    }

    private static string OnOneLine(string nameOrValue) =>
        nameOrValue.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
}
