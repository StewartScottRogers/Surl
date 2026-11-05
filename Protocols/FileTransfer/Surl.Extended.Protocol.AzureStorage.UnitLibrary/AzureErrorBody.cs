using System.Globalization;
using System.Text;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Writes the Blob endpoint's <c>Error</c> body ("Status and error codes"; ADR-0109, decision
/// 5.1): the XML declaration and one line holding <c>Code</c>, then <c>Message</c> - the code's
/// fixed text, a <c>RequestId:</c> line and a <c>Time:</c> line - then each extra element, as UTF-8.
/// </summary>
internal static class AzureErrorBody
{
    /// <summary>
    /// The most UTF-8 bytes of a client value echoed in an element.
    /// </summary>
    public const int MaxEchoedBytes = 1024;

    /// <summary>
    /// Renders the body.
    /// </summary>
    /// <param name="refusal">The error and its extra elements.</param>
    /// <param name="requestId">The answer's <c>x-ms-request-id</c>.</param>
    /// <param name="now">When the answer is made, written on the <c>Time:</c> line.</param>
    /// <returns>The body's bytes.</returns>
    public static byte[] Render(AzureRefusal refusal, string requestId, DateTimeOffset now)
    {
        var message = $"{refusal.Error.Message}\nRequestId:{requestId}\nTime:{now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture)}";
        var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"utf-8\"?><Error>");
        AppendElement(xml, "Code", refusal.Error.Code);
        AppendElement(xml, "Message", message);
        foreach (var element in refusal.Elements)
        {
            AppendElement(xml, element.Name, element.IsClientValue ? Echoed(element.Text) : element.Text);
        }

        xml.Append("</Error>");

        return Encoding.UTF8.GetBytes(xml.ToString());
    }

    /// <summary>
    /// A client value as it is echoed (ADR-0109, decision 5.1): cut to its longest prefix of at
    /// most <see cref="MaxEchoedBytes"/> UTF-8 bytes that ends on a whole character, then
    /// <c>&amp;</c>, <c>&lt;</c> and <c>&gt;</c> escaped.
    /// </summary>
    /// <param name="value">The client's value.</param>
    /// <returns>The value as it is written into the element.</returns>
    public static string Echoed(string value)
    {
        var cut = new StringBuilder();
        var byteCount = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            byteCount += rune.Utf8SequenceLength;
            if (byteCount > MaxEchoedBytes)
            {
                break;
            }

            cut.Append(rune.ToString());
        }

        return cut.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").ToString();
    }

    private static void AppendElement(StringBuilder xml, string name, string text) =>
        xml.Append('<').Append(name).Append('>').Append(text).Append("</").Append(name).Append('>');
}
