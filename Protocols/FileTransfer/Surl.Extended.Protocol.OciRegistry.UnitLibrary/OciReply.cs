using System.Globalization;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Builds the answers to one request: each carries <c>Date</c>, <c>Server</c> and
/// <c>Docker-Distribution-API-Version: registry/2.0</c> (ADR-0115, decision 1), and an error
/// carries the JSON error body, which a <c>HEAD</c> answer leaves out (decision 5.1).
/// </summary>
/// <param name="isHead">Whether the request is a <c>HEAD</c>, whose answers carry no body.</param>
/// <param name="leavesRequestBodyUnread">Whether the request announced a body the answer leaves unread.</param>
/// <param name="now">When the answer is made, sent as <c>Date</c>.</param>
internal sealed class OciReply(bool isHead, bool leavesRequestBodyUnread, DateTimeOffset now)
{
    /// <summary>
    /// The field that tells a client this is a version 2 registry.
    /// </summary>
    public static readonly KeyValuePair<string, string> ApiVersionField = new("Docker-Distribution-API-Version", "registry/2.0");

    /// <summary>
    /// Whether the request is a <c>HEAD</c>.
    /// </summary>
    public bool IsHead => isHead;

    /// <summary>
    /// An answer with the common fields, then <paramref name="fields"/>, then <paramref name="body"/>.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <param name="fields">The fields after the common ones.</param>
    /// <param name="body">The body.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Answer(HttpStatus status, IEnumerable<KeyValuePair<string, string>> fields, HttpAnswerBody body) =>
        new(status, [.. CommonFields(now), .. fields], body, IsRefusal: false, LeavesRequestBodyUnread: leavesRequestBodyUnread);

    /// <summary>
    /// A JSON answer: <c>Content-Type</c>, <c>Content-Length</c>, then the bytes unless the
    /// request is a <c>HEAD</c>.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <param name="contentType">The body's media type.</param>
    /// <param name="fields">The fields after <c>Content-Type</c> and before <c>Content-Length</c>.</param>
    /// <param name="bytes">The body.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Bytes(HttpStatus status, string contentType, IEnumerable<KeyValuePair<string, string>> fields, byte[] bytes) =>
        Answer(
            status,
            [new("Content-Type", contentType), .. fields, new("Content-Length", bytes.Length.ToString(CultureInfo.InvariantCulture))],
            isHead ? HttpAnswerBody.None : new HttpAnswerBody.Bytes(bytes));

    /// <summary>
    /// An error answer with the JSON error body; a <c>HEAD</c> gets the status and no body.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="fields">Fields the error carries beside the common ones, such as <c>Allow</c>.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Error(OciError error, params KeyValuePair<string, string>[] fields) =>
        Bytes(error.Status, "application/json", fields, OciErrorBody.Render(error));

    /// <summary>
    /// An error answer listing <paramref name="error"/> <paramref name="count"/> times.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="count">How many times the body lists it.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Errors(OciError error, int count) =>
        Bytes(error.Status, "application/json", [], OciErrorBody.Render(error, count));

    /// <summary>
    /// An answer with no body: <paramref name="fields"/>, then <c>Content-Length: 0</c> unless the
    /// status is <c>204</c>, which carries none (RFC 9110, section 8.6).
    /// </summary>
    /// <param name="status">The status.</param>
    /// <param name="fields">The fields after the common ones.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Empty(HttpStatus status, params KeyValuePair<string, string>[] fields) =>
        Answer(status, status.Code == 204 ? fields : [.. fields, new("Content-Length", "0")], HttpAnswerBody.None);

    /// <summary>
    /// The same reply for a request whose body has been read to its end, so its answer leaves the
    /// connection open when the request lets it.
    /// </summary>
    /// <returns>The reply.</returns>
    public OciReply AfterBodyRead() => new(isHead, leavesRequestBodyUnread: false, now);

    /// <summary>
    /// <c>Date</c>, <c>Server</c> and the API version field, which every answer starts with.
    /// </summary>
    /// <param name="now">When the answer is made.</param>
    /// <returns>The fields.</returns>
    public static KeyValuePair<string, string>[] CommonFields(DateTimeOffset now) =>
    [
        new("Date", now.ToString("r", CultureInfo.InvariantCulture)),
        new("Server", HttpResponseHead.ServerName),
        ApiVersionField,
    ];
}
