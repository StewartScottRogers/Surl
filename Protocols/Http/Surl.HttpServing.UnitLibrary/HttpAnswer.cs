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
/// <param name="DrainsBeforeClose">
/// Whether, when the answer closes the connection, what the client still sends is read and
/// thrown away after the half-close (the lingering close, ADR-0024). <see langword="false"/>
/// leaves an unread request body unread and the close to the transport, as the S3 server's
/// <c>EntityTooLarge</c> and <c>IncompleteBody</c> close (ADR-0108, decision 7).
/// </param>
public sealed record HttpAnswer(
    HttpStatus Status,
    IReadOnlyList<KeyValuePair<string, string>> Fields,
    HttpAnswerBody Body,
    bool IsRefusal,
    bool LeavesRequestBodyUnread,
    IHttpConnectionTakeover? Takeover = null,
    bool DrainsBeforeClose = true);
