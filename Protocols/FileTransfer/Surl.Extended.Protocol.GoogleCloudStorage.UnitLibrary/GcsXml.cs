using System.Globalization;
using System.Text;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Writes the elements of the listings' XML bodies (ADR-0110, decision 6): text escaped for
/// <c>&amp;</c>, <c>&lt;</c> and <c>&gt;</c>, and times as <c>yyyy-MM-ddTHH:mm:ss.fffZ</c>.
/// </summary>
internal static class GcsXml
{
    /// <summary>
    /// The namespace both listings are written in, as the "GET Bucket (List Objects)" page writes it.
    /// </summary>
    public const string Namespace = "http://doc.s3.amazonaws.com/2006-03-01";

    /// <summary>
    /// Appends <c>&lt;name&gt;text&lt;/name&gt;</c>.
    /// </summary>
    /// <param name="xml">The body being written.</param>
    /// <param name="name">The element's name.</param>
    /// <param name="text">The element's text, escaped here.</param>
    public static void Append(StringBuilder xml, string name, string text) =>
        xml.Append('<').Append(name).Append('>')
            .Append(text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal))
            .Append("</").Append(name).Append('>');

    /// <summary>
    /// Appends the element when <paramref name="text"/> is given.
    /// </summary>
    /// <param name="xml">The body being written.</param>
    /// <param name="name">The element's name.</param>
    /// <param name="text">The element's text, or <see langword="null"/> to write nothing.</param>
    public static void AppendWhenGiven(StringBuilder xml, string name, string? text)
    {
        if (text is not null)
        {
            Append(xml, name, text);
        }
    }

    /// <summary>
    /// A time as the listings write it.
    /// </summary>
    /// <param name="time">The time.</param>
    /// <returns>The time in UTC, to the millisecond, with a <c>Z</c>.</returns>
    public static string Time(DateTimeOffset time) =>
        time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
