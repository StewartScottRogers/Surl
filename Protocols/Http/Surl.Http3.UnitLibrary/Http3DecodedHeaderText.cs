using System.Text;
using Surl.Qpack;

namespace Surl.Http3;

/// <summary>
/// The text a decoded header list is published as (ADR-0169, decision 1): one <c>name: value</c> line per
/// header, each ending CRLF, in the order given, then a blank line.
/// </summary>
internal static class Http3DecodedHeaderText
{
    /// <summary>Renders <paramref name="fieldLines"/> as the published text, in UTF-8.</summary>
    /// <param name="fieldLines">The header list, pseudo-header fields first.</param>
    /// <returns>The text's bytes.</returns>
    public static byte[] Bytes(IEnumerable<QpackFieldLine> fieldLines)
    {
        var text = new StringBuilder();
        foreach (var line in fieldLines)
        {
            text.Append(line.Name).Append(": ").Append(line.Value).Append("\r\n");
        }

        return Encoding.UTF8.GetBytes(text.Append("\r\n").ToString());
    }
}
