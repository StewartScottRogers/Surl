using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Control;

/// <summary>
/// The control endpoint's answers: JSON with <c>Content-Type: application/json</c> and <c>Cache-Control: no-store</c>,
/// <c>204</c> with no body, and errors as <c>{"error", "message"}</c>; each with <c>Date</c> and <c>Server</c>.
/// </summary>
internal sealed class ControlAnswers(TimeProvider timeProvider)
{
    /// <summary>A JSON answer.</summary>
    /// <typeparam name="T">The shape.</typeparam>
    /// <param name="request">The request answered.</param>
    /// <param name="status">The status.</param>
    /// <param name="value">The body's value.</param>
    /// <param name="typeInfo">The shape's generated metadata.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Json<T>(HttpRequest request, HttpStatus status, T value, JsonTypeInfo<T> typeInfo) =>
        JsonAnswer(request, status, JsonSerializer.SerializeToUtf8Bytes(value, typeInfo), isRefusal: false, []);

    /// <summary>A <c>204 No Content</c> answer.</summary>
    /// <param name="request">The request answered.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer NoContent(HttpRequest request) =>
        new(ControlError.NoContent, CommonFields(), HttpAnswerBody.None, IsRefusal: false, LeavesRequestBodyUnread: request.Body.IsAnnounced);

    /// <summary>An error answer.</summary>
    /// <param name="request">The request answered.</param>
    /// <param name="error">The error.</param>
    /// <returns>The answer; a <c>401</c> carries <c>WWW-Authenticate: Bearer</c>.</returns>
    public HttpAnswer Error(HttpRequest request, ControlError error) => JsonAnswer(
        request,
        error.Status,
        JsonSerializer.SerializeToUtf8Bytes(new ControlErrorJson(error.Code, error.Message), ControlJsonContext.Default.ControlErrorJson),
        error.ClosesConnection,
        error.Status.Code == HttpStatus.Unauthorized.Code ? [new("WWW-Authenticate", "Bearer")] : []);

    private HttpAnswer JsonAnswer(HttpRequest request, HttpStatus status, byte[] body, bool isRefusal, KeyValuePair<string, string>[] extraFields)
    {
        KeyValuePair<string, string>[] fields =
        [
            .. CommonFields(),
            .. extraFields,
            new("Content-Type", "application/json"),
            new("Cache-Control", "no-store"),
            new("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture)),
        ];

        return new HttpAnswer(status, fields, new HttpAnswerBody.Bytes(body), isRefusal, LeavesRequestBodyUnread: request.Body.IsAnnounced);
    }

    private KeyValuePair<string, string>[] CommonFields() =>
    [
        new("Date", timeProvider.GetUtcNow().ToString("r", CultureInfo.InvariantCulture)),
        new("Server", HttpResponseHead.ServerName),
    ];
}
