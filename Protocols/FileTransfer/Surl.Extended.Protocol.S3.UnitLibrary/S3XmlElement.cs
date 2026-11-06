using System.Text;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Writes one XML element of a response body: its text with <c>&amp;</c>, <c>&lt;</c> and
/// <c>&gt;</c> escaped, between its start and end tags.
/// </summary>
internal static class S3XmlElement
{
    /// <summary>
    /// Appends <c>&lt;name&gt;text&lt;/name&gt;</c> to <paramref name="xml"/>.
    /// </summary>
    /// <param name="xml">The body being written.</param>
    /// <param name="name">The element's name.</param>
    /// <param name="text">The element's text, unescaped.</param>
    public static void Append(StringBuilder xml, string name, string text) =>
        xml.Append('<').Append(name).Append('>')
            .Append(text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal))
            .Append("</").Append(name).Append('>');
}
