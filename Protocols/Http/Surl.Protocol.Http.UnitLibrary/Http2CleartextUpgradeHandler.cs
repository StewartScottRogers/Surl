using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// The request handler of a cleartext HTTP/1.x connection: answers a request that qualifies for
/// <c>Upgrade: h2c</c> (<see cref="Http2CleartextUpgrade"/>) with
/// <c>101 Switching Protocols</c>, <c>Connection: Upgrade</c> and <c>Upgrade: h2c</c>, and hands
/// the connection to HTTP/2 with that request as stream 1; every other request goes to the
/// answerer as it is (RFC 7540 section 3.2; ADR-0081, decision 3; ADR-0091).
/// </summary>
/// <param name="answerer">Answers every request, the upgraded one on stream 1 included.</param>
/// <param name="context">The exchange.</param>
internal sealed class Http2CleartextUpgradeHandler(IHttpRequestHandler answerer, ExchangeContext context) : IHttpRequestHandler
{
    /// <summary>The upgrade's status: <c>101 Switching Protocols</c>.</summary>
    public static readonly HttpStatus SwitchingProtocols = new(101, "Switching Protocols");

    private static readonly KeyValuePair<string, string>[] UpgradeFields =
    [
        new("Connection", "Upgrade"),
        new("Upgrade", Http2CleartextUpgrade.UpgradeToken),
    ];

    /// <inheritdoc />
    public Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (Http2CleartextUpgrade.ClientSettingsOf(request) is not { } clientSettings)
        {
            return answerer.AnswerAsync(request, cancellationToken);
        }

        context.Log.Note("The request asked to upgrade to h2c; answered 101 and served it as HTTP/2 stream 1.");

        return Task.FromResult(new HttpAnswer(
            SwitchingProtocols,
            UpgradeFields,
            HttpAnswerBody.None,
            IsRefusal: false,
            LeavesRequestBodyUnread: false,
            new Takeover(answerer, context, request, clientSettings)));
    }

    // Owns the connection once the 101 is written: HTTP/2, reading through the HTTP/1.x reader,
    // which may already hold the client's preface.
    private sealed class Takeover(IHttpRequestHandler answerer, ExchangeContext context, HttpRequest request, IReadOnlyList<Http2Setting> clientSettings) : IHttpConnectionTakeover
    {
        public Task TakeOverAsync(IConnection connection, HttpConnectionReader reader, CancellationToken cancellationToken) =>
            new Http2ConnectionServer(new Http2StreamAnswerWriter(answerer, context), context, new HttpConnectionReaderConnection(connection, reader))
                .ServeUpgradedAsync(new Http2RequestHead(request.Method, request.Target, request.Authority, request.Fields, ContentLength: null, ExpectsContinue: false), clientSettings);
    }
}
