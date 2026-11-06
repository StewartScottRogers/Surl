using System.Globalization;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Builds the answers to one request: each carries <c>Date</c>, <c>Server</c> and the request's
/// <c>x-amz-request-id</c> ("Common Response Headers"; ADR-0108, decision 1), and an error carries
/// the <c>Error</c> body, which a <c>HEAD</c> answer leaves out.
/// </summary>
internal sealed class S3Reply
{
    private readonly HttpRequest request;
    private readonly string resource;
    private readonly DateTimeOffset now;

    /// <summary>
    /// Starts the answers to <paramref name="request"/>.
    /// </summary>
    /// <param name="request">The request answered.</param>
    /// <param name="resource">The request's path as the client sent it, echoed in an error's <c>Resource</c>.</param>
    /// <param name="requestId">The request's ID, sent as <c>x-amz-request-id</c> and an error's <c>RequestId</c>.</param>
    /// <param name="now">When the answer is made, sent as <c>Date</c>.</param>
    public S3Reply(HttpRequest request, string resource, string requestId, DateTimeOffset now)
    {
        this.request = request;
        this.resource = resource;
        this.now = now;
        RequestId = requestId;
    }

    /// <summary>
    /// The request's ID.
    /// </summary>
    public string RequestId { get; }

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
            new("x-amz-request-id", RequestId),
        ];

        return new HttpAnswer(status, [.. common, .. fields], body, IsRefusal: false, LeavesRequestBodyUnread: request.Body.IsAnnounced);
    }

    /// <summary>
    /// The refusal of a request whose head could not be read: <c>Date</c>, <c>Server</c>,
    /// <c>x-amz-request-id</c>, <c>Content-Type: application/xml</c> and the <c>Error</c> body,
    /// whose <c>Resource</c> is empty because no request target was read.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="requestId">The refusal's ID, sent as <c>x-amz-request-id</c> and the body's <c>RequestId</c>.</param>
    /// <param name="now">When the refusal is made, sent as <c>Date</c>.</param>
    /// <returns>The refusal.</returns>
    public static HttpAnswer UnreadRequestError(S3Error error, string requestId, DateTimeOffset now)
    {
        var body = S3ErrorBody.Render(error, string.Empty, requestId, []);
        KeyValuePair<string, string>[] fields =
        [
            new("Date", now.ToString("r", CultureInfo.InvariantCulture)),
            new("Server", HttpResponseHead.ServerName),
            new("x-amz-request-id", requestId),
            new("Content-Type", "application/xml"),
            new("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture)),
        ];

        return new HttpAnswer(error.Status, fields, new HttpAnswerBody.Bytes(body), IsRefusal: true, LeavesRequestBodyUnread: false);
    }

    /// <summary>
    /// An error answer: its status, <c>Content-Type: application/xml</c>, and the <c>Error</c>
    /// body with its length unless the request is a <c>HEAD</c> (HeadObject: "the response to a
    /// HEAD request has no body"). An error that <see cref="S3Error.ClosesWithoutDrain"/> is a
    /// refusal that closes the connection without draining the request body (ADR-0108, decision 7).
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="extraElements">The elements the code carries beside the common ones.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Error(S3Error error, params KeyValuePair<string, string>[] extraElements)
    {
        var answer = ErrorAnswer(error, extraElements);

        return error.ClosesWithoutDrain ? answer with { IsRefusal = true, DrainsBeforeClose = false } : answer;
    }

    private HttpAnswer ErrorAnswer(S3Error error, KeyValuePair<string, string>[] extraElements)
    {
        if (IsHead)
        {
            return Answer(error.Status, [new("Content-Type", "application/xml")], HttpAnswerBody.None);
        }

        var body = S3ErrorBody.Render(error, resource, RequestId, extraElements);

        return Answer(
            error.Status,
            [new("Content-Type", "application/xml"), new("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture))],
            new HttpAnswerBody.Bytes(body));
    }
}
