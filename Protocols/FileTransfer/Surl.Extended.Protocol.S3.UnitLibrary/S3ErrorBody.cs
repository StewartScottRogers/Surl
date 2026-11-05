using System.Text;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Writes the S3 <c>Error</c> body ("Error responses"; ADR-0108, decision 1.1): the XML
/// declaration, then one line holding <c>Code</c>, <c>Message</c>, any extra element the code
/// carries, <c>Resource</c> and <c>RequestId</c>, as UTF-8.
/// </summary>
internal static class S3ErrorBody
{
    /// <summary>
    /// The most characters of a client value echoed in an element.
    /// </summary>
    public const int MaxEchoedLength = 1024;

    /// <summary>
    /// Renders the body.
    /// </summary>
    /// <param name="error">The error, whose code and message are fixed text.</param>
    /// <param name="resource">The request's path as the client sent it, still percent-encoded.</param>
    /// <param name="requestId">The answer's <c>x-amz-request-id</c>.</param>
    /// <param name="extraElements">The elements the code carries beside the common ones, such as
    /// <c>Key</c>, with their client values.</param>
    /// <returns>The body's bytes.</returns>
    public static byte[] Render(S3Error error, string resource, string requestId, IReadOnlyList<KeyValuePair<string, string>> extraElements)
    {
        var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Error>");
        AppendElement(xml, "Code", error.Code);
        AppendElement(xml, "Message", error.Message);
        foreach (var element in extraElements)
        {
            AppendElement(xml, element.Key, Echoed(element.Value));
        }

        AppendElement(xml, "Resource", Echoed(resource));
        AppendElement(xml, "RequestId", requestId);
        xml.Append("</Error>");

        return Encoding.UTF8.GetBytes(xml.ToString());
    }

    /// <summary>
    /// A client value as it is echoed (ADR-0006, section 3): every character outside printable
    /// ASCII replaced by <c>?</c>, cut to <see cref="MaxEchoedLength"/> characters, then
    /// <c>&amp;</c>, <c>&lt;</c> and <c>&gt;</c> escaped.
    /// </summary>
    /// <param name="value">The client's value.</param>
    /// <returns>The value as it is written into the element.</returns>
    public static string Echoed(string value)
    {
        var cut = value.Length > MaxEchoedLength ? value[..MaxEchoedLength] : value;
        var printable = new StringBuilder(cut.Length);
        foreach (var character in cut)
        {
            printable.Append(character is >= ' ' and <= '~' ? character : '?');
        }

        return printable.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").ToString();
    }

    private static void AppendElement(StringBuilder xml, string name, string text) =>
        xml.Append('<').Append(name).Append('>').Append(text).Append("</").Append(name).Append('>');
}
