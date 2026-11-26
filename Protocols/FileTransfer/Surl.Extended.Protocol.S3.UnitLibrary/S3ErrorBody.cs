using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Writes the S3 <c>Error</c> body ("Error responses"; ADR-0108, decision 1.1 and Amendment 12):
/// the XML declaration, then <c>Code</c>, <c>Message</c>, a signature mismatch's
/// <c>StringToSign</c> and <c>CanonicalRequest</c>, any extra element the code carries,
/// <c>Resource</c> and <c>RequestId</c>, as UTF-8, with no whitespace between elements.
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
    /// <param name="error">The error, whose code and message are fixed text, with its signature
    /// mismatch if it carries one.</param>
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
        AppendSignatureMismatch(xml, error.SignatureMismatch);
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
    public static string Echoed(string value) =>
        Escaped(value.Length > MaxEchoedLength ? value[..MaxEchoedLength] : value, keepsLineFeeds: false);

    /// <summary>
    /// The canonical request or the string to sign as it is echoed (ADR-0108, Amendment 12): its
    /// line feeds kept, every other character outside printable ASCII replaced by <c>?</c>, then
    /// <c>&amp;</c>, <c>&lt;</c> and <c>&gt;</c> escaped, and not cut.
    /// </summary>
    /// <param name="signedText">The text, built from the client's request.</param>
    /// <returns>The text as it is written into the element.</returns>
    public static string EchoedLines(string signedText) => Escaped(signedText, keepsLineFeeds: true);

    private static string Escaped(string value, bool keepsLineFeeds)
    {
        var printable = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            var isKept = character is >= ' ' and <= '~' || (keepsLineFeeds && character == '\n');
            printable.Append(isKept ? character : '?');
        }

        return printable.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").ToString();
    }

    private static void AppendSignatureMismatch(StringBuilder xml, AwsSigV4SignatureMismatch? mismatch)
    {
        if (mismatch is null)
        {
            return;
        }

        AppendElement(xml, "StringToSign", EchoedLines(mismatch.StringToSign));
        AppendElement(xml, "CanonicalRequest", EchoedLines(mismatch.CanonicalRequest));
    }

    private static void AppendElement(StringBuilder xml, string name, string text) =>
        xml.Append('<').Append(name).Append('>').Append(text).Append("</").Append(name).Append('>');
}
