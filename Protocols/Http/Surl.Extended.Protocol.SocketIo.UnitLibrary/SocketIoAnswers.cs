using System.Globalization;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// The HTTP answers the Socket.IO server writes, each with <c>Date</c> from the exchange's clock and
/// <c>Content-Length</c> (ADR-0120, decisions 1 and 2).
/// </summary>
/// <param name="context">The exchange whose clock dates the answers.</param>
internal sealed class SocketIoAnswers(ExchangeContext context)
{
    /// <summary>
    /// <c>400 Bad Request</c> with <see cref="EngineIoRefusal"/>'s body for <paramref name="code"/>.
    /// </summary>
    public HttpAnswer Refused(HttpRequest request, int code) =>
        WithBody(request, HttpStatus.BadRequest, "application/json", EngineIoRefusal.Body(code));

    /// <summary>
    /// <paramref name="status"/> with no body and any <paramref name="fields"/> after the length.
    /// </summary>
    public HttpAnswer Empty(HttpRequest request, HttpStatus status, IReadOnlyList<KeyValuePair<string, string>>? fields = null) =>
        new(status, [DateField(), ContentLength(0), .. fields ?? []], HttpAnswerBody.None, IsRefusal: false, LeavesRequestBodyUnread: request.Body.IsAnnounced);

    /// <summary>
    /// <paramref name="status"/> with <paramref name="body"/> as <paramref name="contentType"/>.
    /// </summary>
    public HttpAnswer WithBody(HttpRequest request, HttpStatus status, string contentType, byte[] body) =>
        new(
            status,
            [DateField(), ContentLength(body.Length), new("Content-Type", contentType)],
            new HttpAnswerBody.Bytes(body),
            IsRefusal: false,
            LeavesRequestBodyUnread: request.Body.IsAnnounced);

    /// <summary>
    /// The <c>Date</c> field, now.
    /// </summary>
    public KeyValuePair<string, string> DateField() =>
        new("Date", context.TimeProvider.GetUtcNow().ToString("r", CultureInfo.InvariantCulture));

    private static KeyValuePair<string, string> ContentLength(int length) =>
        new("Content-Length", length.ToString(CultureInfo.InvariantCulture));
}
