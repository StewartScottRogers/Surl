using System.Globalization;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Builds the answers to one request: each carries <c>Date</c> and <c>Server</c> (ADR-0110,
/// decision 1.1), and an error carries the <c>Error</c> body, which a <c>HEAD</c> answer leaves out.
/// </summary>
/// <param name="request">The request answered.</param>
/// <param name="now">When the answer is made, sent as <c>Date</c>.</param>
internal sealed class GcsReply(HttpRequest request, DateTimeOffset now)
{
    /// <summary>
    /// The <c>Content-Type</c> of every XML body the server writes.
    /// </summary>
    public const string XmlContentType = "application/xml; charset=UTF-8";

    /// <summary>
    /// When the answer is made.
    /// </summary>
    public DateTimeOffset Now => now;

    /// <summary>
    /// Whether the request is a <c>HEAD</c>, whose answers carry no body.
    /// </summary>
    public bool IsHead => request.Method == "HEAD";

    /// <summary>
    /// An answer with the common fields, then <paramref name="fields"/>, then <paramref name="body"/>.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <param name="fields">The fields after the common ones.</param>
    /// <param name="body">The body.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Answer(HttpStatus status, IEnumerable<KeyValuePair<string, string>> fields, HttpAnswerBody body)
    {
        KeyValuePair<string, string>[] common =
        [
            new("Date", now.ToString("r", CultureInfo.InvariantCulture)),
            new("Server", HttpResponseHead.ServerName),
        ];

        return new HttpAnswer(status, [.. common, .. fields], body, IsRefusal: false, LeavesRequestBodyUnread: request.Body.IsAnnounced);
    }

    /// <summary>
    /// A <c>200</c> XML answer with its <c>Content-Type</c> and <c>Content-Length</c>.
    /// </summary>
    /// <param name="xml">The body's bytes.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Xml(byte[] xml) =>
        Answer(
            HttpStatus.Ok,
            [new("Content-Type", XmlContentType), new("Content-Length", xml.Length.ToString(CultureInfo.InvariantCulture))],
            new HttpAnswerBody.Bytes(xml));

    /// <summary>
    /// A <c>200</c> JSON answer with its <c>Content-Type</c> and <c>Content-Length</c>.
    /// </summary>
    /// <param name="json">The body's bytes.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Json(byte[] json) =>
        Answer(
            HttpStatus.Ok,
            [new("Content-Type", GcsJson.ContentType), new("Content-Length", json.Length.ToString(CultureInfo.InvariantCulture))],
            new HttpAnswerBody.Bytes(json));

    /// <summary>
    /// The refusal of a request whose head could not be read: <c>Date</c>, <c>Server</c>,
    /// <c>Content-Type</c>, <c>Content-Length</c> and the <c>Error</c> body.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="now">When the refusal is made, sent as <c>Date</c>.</param>
    /// <returns>The refusal.</returns>
    public static HttpAnswer UnreadRequestError(GcsError error, DateTimeOffset now)
    {
        var body = GcsErrorBody.Render(error);
        KeyValuePair<string, string>[] fields =
        [
            new("Date", now.ToString("r", CultureInfo.InvariantCulture)),
            new("Server", HttpResponseHead.ServerName),
            new("Content-Type", XmlContentType),
            new("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture)),
        ];

        return new HttpAnswer(error.Status, fields, new HttpAnswerBody.Bytes(body), IsRefusal: true, LeavesRequestBodyUnread: false);
    }

    /// <summary>
    /// A JSON error answer (ADR-0148, decision 5): its status, <paramref name="extraFields"/>, and the
    /// one-line error body - also to a <c>HEAD</c>, which the JSON API does not have - except for
    /// <c>304</c>, which has none.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="extraFields">Fields sent before the body's, such as <c>WWW-Authenticate</c>.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer JsonError(GcsJsonError error, params KeyValuePair<string, string>[] extraFields)
    {
        if (!error.HasBody)
        {
            return Answer(error.Status, extraFields, HttpAnswerBody.None);
        }

        var body = GcsJsonErrorBody.Render(error);

        return Answer(
            error.Status,
            [.. extraFields, new("Content-Type", GcsJson.ContentType), new("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture))],
            new HttpAnswerBody.Bytes(body));
    }

    /// <summary>
    /// An error answer: its status, <paramref name="extraFields"/>, and the <c>Error</c> body
    /// unless the request is a <c>HEAD</c> ("HTTP status and error codes").
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="extraFields">Fields sent before the body's, such as <c>WWW-Authenticate</c>.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Error(GcsError error, params KeyValuePair<string, string>[] extraFields)
    {
        if (IsHead)
        {
            return Answer(error.Status, [.. extraFields, new("Content-Type", XmlContentType)], HttpAnswerBody.None);
        }

        var body = GcsErrorBody.Render(error);

        return Answer(
            error.Status,
            [.. extraFields, new("Content-Type", XmlContentType), new("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture))],
            new HttpAnswerBody.Bytes(body));
    }
}
