using System.Globalization;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// Writes the MCP server's HTTP answers: every one carries <c>Date</c> on the injected clock and <c>Server</c>,
/// then its own fields, then <c>Content-Length</c>. The endpoint and the authorization server both answer
/// through it.
/// </summary>
/// <param name="timeProvider">The clock <c>Date</c> is read from.</param>
internal sealed class McpHttpAnswerWriter(TimeProvider timeProvider)
{
    /// <summary>The JSON media type.</summary>
    public const string JsonMediaType = "application/json";

    /// <summary>An answer with a JSON body.</summary>
    /// <param name="request">The request answered.</param>
    /// <param name="status">The status.</param>
    /// <param name="body">The JSON.</param>
    /// <param name="fields">Fields written after <c>Content-Type</c>.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Json(HttpRequest request, HttpStatus status, byte[] body, IReadOnlyList<KeyValuePair<string, string>>? fields = null) =>
        Answer(request, status, JsonMediaType, body, fields ?? []);

    /// <summary>
    /// An answer with a JSON body that holds or refuses credentials, which no cache may keep: <c>Cache-Control:
    /// no-store</c> (RFC 6749 section 5.1, RFC 7591 section 3.2.1).
    /// </summary>
    /// <param name="request">The request answered.</param>
    /// <param name="status">The status.</param>
    /// <param name="body">The JSON.</param>
    /// <param name="fields">Fields written after <c>Cache-Control</c>.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer NoStoreJson(HttpRequest request, HttpStatus status, byte[] body, IReadOnlyList<KeyValuePair<string, string>>? fields = null) =>
        Answer(request, status, JsonMediaType, body, [new("Cache-Control", "no-store"), .. fields ?? []]);

    /// <summary>An answer with a body.</summary>
    /// <param name="request">The request answered.</param>
    /// <param name="status">The status.</param>
    /// <param name="mediaType">The body's <c>Content-Type</c>.</param>
    /// <param name="body">The body.</param>
    /// <param name="fields">Fields written after <c>Content-Type</c>.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Answer(HttpRequest request, HttpStatus status, string mediaType, byte[] body, IReadOnlyList<KeyValuePair<string, string>> fields) =>
        new(
            status,
            [.. CommonFields(), new("Content-Type", mediaType), .. fields, new("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture))],
            new HttpAnswerBody.Bytes(body),
            IsRefusal: false,
            LeavesRequestBodyUnread: request.Body.IsAnnounced);

    /// <summary>
    /// A <c>200</c> whose body is written while it is produced: no <c>Content-Length</c>, so the HTTP/1.1 writer
    /// sends it chunked (RFC 9112 section 7.1) and the head goes out before the body is ready.
    /// </summary>
    /// <param name="request">The request answered.</param>
    /// <param name="mediaType">The body's <c>Content-Type</c>.</param>
    /// <param name="writeAsync">Produces the body.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Streamed(HttpRequest request, string mediaType, Func<HttpAnswerChunkWriter, CancellationToken, Task> writeAsync) =>
        new(
            HttpStatus.Ok,
            [.. CommonFields(), new("Content-Type", mediaType)],
            new HttpAnswerBody.Streamed(writeAsync),
            IsRefusal: false,
            LeavesRequestBodyUnread: request.Body.IsAnnounced);

    /// <summary>An answer with no body.</summary>
    /// <param name="request">The request answered.</param>
    /// <param name="status">The status.</param>
    /// <param name="fields">Fields written before <c>Content-Length</c>.</param>
    /// <param name="contentLength">Whether <c>Content-Length: 0</c> is written (not on a <c>204</c>).</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Empty(HttpRequest request, HttpStatus status, IReadOnlyList<KeyValuePair<string, string>>? fields = null, bool contentLength = true) =>
        new(
            status,
            [.. CommonFields(), .. fields ?? [], .. contentLength ? ContentLengthZero : []],
            HttpAnswerBody.None,
            IsRefusal: false,
            LeavesRequestBodyUnread: request.Body.IsAnnounced);

    /// <summary>A refusal while reading the body: the connection closes after it.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Refusal(HttpStatus status) =>
        new(status, [.. CommonFields(), .. ContentLengthZero], HttpAnswerBody.None, IsRefusal: true, LeavesRequestBodyUnread: true);

    private KeyValuePair<string, string>[] CommonFields() =>
    [
        new("Date", timeProvider.GetUtcNow().ToString("r", CultureInfo.InvariantCulture)),
        new("Server", HttpResponseHead.ServerName),
    ];

    private static readonly KeyValuePair<string, string>[] ContentLengthZero = [new("Content-Length", "0")];
}
