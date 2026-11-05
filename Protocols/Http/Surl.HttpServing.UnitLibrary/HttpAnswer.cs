using Surl.HttpMessage;

namespace Surl.HttpServing;

/// <summary>
/// What an <see cref="IHttpRequestHandler"/> decided to answer a request with, before any HTTP
/// version's bytes are written for it (ADR-0081, decision 8; ADR-0084, decision 1).
/// </summary>
/// <param name="Status">The response status.</param>
/// <param name="Fields">
/// The response fields, in the order sent; the fields a version's framing owns, such as
/// HTTP/1.x's <c>Connection</c>, are left to that version's writer.
/// </param>
/// <param name="Body">The response body.</param>
/// <param name="IsRefusal">
/// Whether the request is refused: the client is told the server reads no more requests
/// after this one.
/// </param>
/// <param name="LeavesRequestBodyUnread">Whether the request's announced body was left unread.</param>
/// <param name="Takeover">
/// What owns the connection once the answer's head is written - an upgrade answered
/// <c>101 Switching Protocols</c> - or <see langword="null"/> when the connection stays HTTP's.
/// </param>
public sealed record HttpAnswer(
    HttpStatus Status,
    IReadOnlyList<KeyValuePair<string, string>> Fields,
    HttpAnswerBody Body,
    bool IsRefusal,
    bool LeavesRequestBodyUnread,
    IHttpConnectionTakeover? Takeover = null);
