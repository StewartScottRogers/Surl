using System.Text;
using Surl.HttpMessage;

namespace Surl.HttpServing;

/// <summary>
/// The short fixed text body the HTTP server's own 4xx and 5xx answers carry (ADR-0186, decisions 2 and 3):
/// the status code, a space, the reason phrase, then one line feed, as <c>text/plain; charset=utf-8</c>.
/// </summary>
public static class HttpErrorText
{
    /// <summary>
    /// The media type of the body.
    /// </summary>
    public const string ContentType = "text/plain; charset=utf-8";

    /// <summary>
    /// Whether an answer with this status carries the text: every status of 400 and above.
    /// </summary>
    /// <param name="status">The answer's status.</param>
    /// <returns><see langword="true"/> for a 4xx or 5xx.</returns>
    public static bool Carries(HttpStatus status) => status.Code >= 400;

    /// <summary>
    /// The text's bytes for a status: <c>404 Not Found</c> and a line feed; the code alone when the status has no reason phrase.
    /// </summary>
    /// <param name="status">The answer's status.</param>
    /// <returns>The body's bytes.</returns>
    public static byte[] BytesOf(HttpStatus status) =>
        Encoding.ASCII.GetBytes(status.ReasonPhrase.Length == 0 ? $"{status.Code}\n" : $"{status.Code} {status.ReasonPhrase}\n");

    /// <summary>
    /// The body a 4xx or 5xx answer is written with: the text, or none for a <c>HEAD</c>, whose head is the
    /// same as a GET's (RFC 9110 section 9.3.2).
    /// </summary>
    /// <param name="status">The answer's status.</param>
    /// <param name="isHead">Whether the request was a <c>HEAD</c>.</param>
    /// <returns>The text as bytes, or <see cref="HttpAnswerBody.None"/>.</returns>
    public static HttpAnswerBody BodyOf(HttpStatus status, bool isHead) =>
        isHead ? HttpAnswerBody.None : new HttpAnswerBody.Bytes(BytesOf(status));

    /// <summary>
    /// The fields describing the text: <c>Content-Type</c> and <c>Content-Length</c>.
    /// </summary>
    /// <param name="status">The answer's status.</param>
    /// <returns>The two fields, in the order sent.</returns>
    public static IReadOnlyList<KeyValuePair<string, string>> FieldsOf(HttpStatus status) =>
    [
        new("Content-Type", ContentType),
        new("Content-Length", BytesOf(status).Length.ToString(System.Globalization.CultureInfo.InvariantCulture)),
    ];
}
