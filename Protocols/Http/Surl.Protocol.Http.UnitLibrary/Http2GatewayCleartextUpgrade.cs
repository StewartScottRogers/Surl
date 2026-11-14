using Surl.Http2;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// The request handler of the <c>http/2</c> gateway half's plaintext port once a connection has not
/// opened with the HTTP/2 preface (ADR-0154 decision 2). A request that qualifies for
/// <c>Upgrade: h2c</c> (<see cref="Http2CleartextUpgrade"/>, a body allowed) has its body read by its
/// <c>Content-Length</c>, is answered <c>101 Switching Protocols</c> and is served as HTTP/2 stream 1,
/// forwarded like every other stream; any other request is forwarded as HTTP/1.1 and its reply passed
/// back as HTTP/1.1.
/// </summary>
/// <param name="backend">The case's <c>http</c> half.</param>
/// <param name="context">The exchange.</param>
internal sealed class Http2GatewayCleartextUpgrade(NghttpxGatewayBackend backend, ExchangeContext context) : IHttpRequestHandler
{
    private static readonly KeyValuePair<string, string>[] UpgradeFields =
    [
        new("Connection", "Upgrade"),
        new("Upgrade", Http2CleartextUpgrade.UpgradeToken),
    ];

    /// <inheritdoc />
    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        byte[]? body = await ReadBodyAsync(request.Body, cancellationToken);
        if (body is null)
        {
            context.Log.Note("The request body could not be read whole; answered 400 and closed.");

            return Refusal(new HttpStatus(400, "Bad Request"));
        }

        if (Http2CleartextUpgrade.ClientSettingsOf(request, allowsRequestBody: true) is not { } clientSettings)
        {
            return await ForwardAsync(request, body, cancellationToken);
        }

        context.Log.Note("The request asked to upgrade to h2c; answered 101 and served it as HTTP/2 stream 1.");
        Http2RequestHead head = new(request.Method, request.Target, request.Authority, request.Fields, ContentLength: null, ExpectsContinue: false);

        return new HttpAnswer(
            Http2CleartextUpgradeHandler.SwitchingProtocols,
            UpgradeFields,
            HttpAnswerBody.None,
            IsRefusal: false,
            LeavesRequestBodyUnread: false,
            new Takeover(backend, context, head, clientSettings, body));
    }

    // Null when the body is not read whole: it uses a coding that cannot be read, or the client ended it early.
    private static async Task<byte[]?> ReadBodyAsync(IHttpRequestBody requestBody, CancellationToken cancellationToken)
    {
        if (!requestBody.IsAnnounced)
        {
            return [];
        }

        if (!requestBody.IsReadable)
        {
            return null;
        }

        using MemoryStream body = new();
        HttpRequestBodyDiscardOutcome outcome = await requestBody.CopyToAsync(body, cancellationToken);

        return outcome == HttpRequestBodyDiscardOutcome.Discarded ? body.ToArray() : null;
    }

    private async Task<HttpAnswer> ForwardAsync(HttpRequest request, byte[] body, CancellationToken cancellationToken)
    {
        byte[] forwarded = NghttpxGatewayRequestTranslator.Translate(
            NghttpxGatewayVersion.Http2, request.Method, "http", request.Authority, request.Target, request.Fields, body);
        bool isHead = request.Method == "HEAD";

        byte[] reply = await backend.ReplyToAsync(forwarded, isHead, cancellationToken);
        if (NghttpxGatewayReplyFramingReader.Read(reply, isHead) == NghttpxGatewayReplyFraming.Short)
        {
            context.Log.Note("The backend closed in the middle of the reply body; the connection was aborted.");

            return new HttpAnswer(new HttpStatus(500, string.Empty), [], HttpAnswerBody.None, IsRefusal: true, LeavesRequestBodyUnread: false, DropsExchange: true);
        }

        NghttpxGatewayResponse response = NghttpxGatewayResponseTranslator.Translate(NghttpxGatewayVersion.Http2, reply) ?? NghttpxGatewayBackend.BadGateway();

        // The writer frames the body itself, so the backend's content-length is not repeated.
        return new HttpAnswer(
            new HttpStatus(response.Status, ReasonPhraseOf(reply, response.Status)),
            [.. response.Fields.Where(field => !string.Equals(field.Key, "content-length", StringComparison.Ordinal))],
            new HttpAnswerBody.Bytes(response.Body),
            IsRefusal: false,
            LeavesRequestBodyUnread: false);
    }

    // The text after the status code on the reply's status line; empty for the 502 the gateway makes itself.
    private static string ReasonPhraseOf(byte[] reply, int status)
    {
        string statusLine = System.Text.Encoding.Latin1.GetString(reply).Split("\r\n")[0];
        string prefix = $"HTTP/1.1 {status} ";

        return statusLine.StartsWith(prefix, StringComparison.Ordinal) ? statusLine[prefix.Length..] : string.Empty;
    }

    private static HttpAnswer Refusal(HttpStatus status) =>
        new(status, [new("Connection", "close")], HttpAnswerBody.None, IsRefusal: true, LeavesRequestBodyUnread: false);

    // Owns the connection once the 101 is written: HTTP/2, reading through the HTTP/1.x reader,
    // which may already hold the client's preface.
    private sealed class Takeover(
        NghttpxGatewayBackend backend,
        ExchangeContext context,
        Http2RequestHead head,
        IReadOnlyList<Http2Setting> clientSettings,
        byte[] body) : IHttpConnectionTakeover
    {
        public Task TakeOverAsync(IConnection connection, HttpConnectionReader reader, CancellationToken cancellationToken) =>
            new Http2ConnectionServer(new Http2GatewayStreamForwarder(backend, body), context, new HttpConnectionReaderConnection(connection, reader))
                .ServeUpgradedAsync(head, clientSettings);
    }
}
