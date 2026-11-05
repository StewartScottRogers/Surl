using System.Globalization;

namespace Surl.HttpServing;

/// <summary>
/// The smallest server an extended HTTP server could be: it keeps each request's body, when
/// one is announced, and answers <c>200 OK</c> with that body and its <c>Content-Length</c>, or
/// with <see cref="Takeover"/> after a <c>101</c> when one is set. A body that cannot be read
/// whole is refused <c>400</c>. It records every request it was handed.
/// </summary>
internal sealed class UnitTestEchoRequestHandler : IHttpRequestHandler
{
    private readonly List<HttpRequest> requests = [];

    public IReadOnlyList<HttpRequest> Requests => requests;

    public IHttpConnectionTakeover? Takeover { get; init; }

    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        requests.Add(request);
        if (Takeover is not null)
        {
            return new HttpAnswer(new HttpStatus(101, "Switching Protocols"), [new("Upgrade", "unit-test"), new("Connection", "Upgrade")], HttpAnswerBody.None, IsRefusal: false, LeavesRequestBodyUnread: false, Takeover);
        }

        using var body = new MemoryStream();
        var outcome = request.Body.IsAnnounced
            ? await request.Body.CopyToAsync(body, cancellationToken)
            : HttpRequestBodyDiscardOutcome.Discarded;
        if (outcome != HttpRequestBodyDiscardOutcome.Discarded)
        {
            return new HttpAnswer(HttpStatus.BadRequest, [new("Content-Length", "0")], HttpAnswerBody.None, IsRefusal: true, LeavesRequestBodyUnread: false);
        }

        var bytes = body.ToArray();

        return new HttpAnswer(
            HttpStatus.Ok,
            [new("Content-Length", bytes.Length.ToString(CultureInfo.InvariantCulture))],
            new HttpAnswerBody.Bytes(bytes),
            IsRefusal: false,
            LeavesRequestBodyUnread: false);
    }
}
