using System.Globalization;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Builds the answers to one request (ADR-0109, decision 1): each carries <c>Date</c>,
/// <c>Server</c>, <c>x-ms-request-id</c>, <c>x-ms-version: 2025-01-05</c> and the echoed
/// <c>x-ms-client-request-id</c>; an error also carries <c>x-ms-error-code</c> and the <c>Error</c>
/// body, which a <c>HEAD</c> answer leaves out (decision 5.1).
/// </summary>
internal sealed class AzureReply
{
    /// <summary>
    /// The one version the server implements and names in every answer.
    /// </summary>
    public const string Version = "2025-01-05";

    /// <summary>
    /// The most bytes of an <c>x-ms-client-request-id</c> echoed.
    /// </summary>
    public const int MaxClientRequestIdBytes = 1024;

    private readonly HttpRequest request;
    private readonly DateTimeOffset now;

    /// <summary>
    /// Starts the answers to <paramref name="request"/>.
    /// </summary>
    /// <param name="request">The request answered.</param>
    /// <param name="requestId">The request's ID, sent as <c>x-ms-request-id</c> and on an error's <c>RequestId:</c> line.</param>
    /// <param name="now">When the answer is made.</param>
    public AzureReply(HttpRequest request, string requestId, DateTimeOffset now)
    {
        this.request = request;
        this.now = now;
        RequestId = requestId;
    }

    /// <summary>
    /// The request's ID.
    /// </summary>
    public string RequestId { get; }

    /// <summary>
    /// When the answer is made.
    /// </summary>
    public DateTimeOffset Now => now;

    /// <summary>
    /// Whether the request is a <c>HEAD</c>, whose answers carry no body.
    /// </summary>
    public bool IsHead => request.Method == "HEAD";

    /// <summary>
    /// The fields every answer starts with.
    /// </summary>
    /// <param name="requestId">The answer's request ID.</param>
    /// <param name="now">When the answer is made.</param>
    /// <returns>The fields.</returns>
    public static List<KeyValuePair<string, string>> CommonFields(string requestId, DateTimeOffset now) =>
    [
        new("Date", now.ToString("r", CultureInfo.InvariantCulture)),
        new("Server", HttpResponseHead.ServerName),
        new("x-ms-request-id", requestId),
        new("x-ms-version", Version),
    ];

    /// <summary>
    /// A fresh request ID: a lower-case GUID from the cryptographic random number generator.
    /// </summary>
    /// <returns>The ID.</returns>
    public static string NewRequestId() => new Guid(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToString("D");

    /// <summary>
    /// An answer with the common fields, then <paramref name="fields"/>, then <paramref name="body"/>.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <param name="fields">The fields after the common ones.</param>
    /// <param name="body">The body.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Answer(HttpStatus status, IEnumerable<KeyValuePair<string, string>> fields, HttpAnswerBody body)
    {
        var common = CommonFields(RequestId, now);
        if (EchoedClientRequestId() is { } clientRequestId)
        {
            common.Add(new("x-ms-client-request-id", clientRequestId));
        }

        return new HttpAnswer(status, [.. common, .. fields], body, IsRefusal: false, LeavesRequestBodyUnread: request.Body.IsAnnounced);
    }

    /// <summary>
    /// An error answer with no extra field.
    /// </summary>
    /// <param name="refusal">The error and its extra elements.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Error(AzureRefusal refusal) => Error(refusal, []);

    /// <summary>
    /// An error answer: its status, <c>x-ms-error-code</c>, <paramref name="extraFields"/>, and,
    /// unless the request is a <c>HEAD</c>, <c>Content-Type: application/xml</c> and the body.
    /// </summary>
    /// <param name="refusal">The error and its extra elements.</param>
    /// <param name="extraFields">Fields after <c>x-ms-error-code</c>, such as <c>WWW-Authenticate</c>.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Error(AzureRefusal refusal, IReadOnlyList<KeyValuePair<string, string>> extraFields)
    {
        List<KeyValuePair<string, string>> fields = [new("x-ms-error-code", refusal.Error.Code), .. extraFields];
        if (IsHead)
        {
            return Answer(refusal.Error.Status, fields, HttpAnswerBody.None);
        }

        var body = AzureErrorBody.Render(refusal, RequestId, now);
        fields.Add(new("Content-Type", "application/xml"));
        fields.Add(new("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture)));

        return Answer(refusal.Error.Status, fields, new HttpAnswerBody.Bytes(body));
    }

    // Echoed when at most 1024 bytes of visible ASCII (ADR-0109, decision 1).
    private string? EchoedClientRequestId() =>
        AzureRequestFields.Value(request.Fields, "x-ms-client-request-id") is { Length: > 0 and <= MaxClientRequestIdBytes } value
            && value.All(character => character is >= '!' and <= '~')
            ? value
            : null;
}
