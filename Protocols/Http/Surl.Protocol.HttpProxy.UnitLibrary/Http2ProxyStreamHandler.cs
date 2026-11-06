using System.Globalization;
using System.Text;
using Surl.Hpack;
using Surl.Http2;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// Answers each stream of an HTTP/2 connection to the HTTPS proxy (ADR-0088 decision 6 as
/// Amendment 5 amends it), what curl's <c>--proxy-http2</c> opens: a <c>CONNECT</c> stream
/// (RFC 9113 section 8.5) is tunnelled, and an <c>http</c> request is forwarded to its origin
/// over HTTP/1.1 and its response relayed back on the stream.
/// </summary>
/// <remarks>
/// Every stream is first judged by the connection's proxy login, as on HTTP/1.1, one at a time.
/// Each forwarded stream opens its own origin connection and closes it when the stream ends.
/// </remarks>
internal sealed class Http2ProxyStreamHandler : IHttp2StreamHandler, IDisposable
{
    private readonly ExchangeContext context;
    private readonly IOriginConnector originConnector;
    private readonly IHttpAuthenticationSession authenticationSession;
    private readonly SemaphoreSlim loginGate = new(1, 1);

    /// <summary>
    /// Creates the handler for one HTTP/2 connection to the proxy.
    /// </summary>
    /// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
    /// <param name="originConnector">How origins are reached.</param>
    /// <param name="authenticationSession">The connection's proxy login session.</param>
    public Http2ProxyStreamHandler(ExchangeContext context, IOriginConnector originConnector, IHttpAuthenticationSession authenticationSession)
    {
        this.context = context;
        this.originConnector = originConnector;
        this.authenticationSession = authenticationSession;
    }

    private enum BodySendOutcome
    {
        Sent,
        TooLarge,
        ContentLengthMismatch,
        EndedEarly,
        OriginClosed,
    }

    /// <inheritdoc/>
    public async Task ServeStreamAsync(Http2RequestStream stream, CancellationToken cancellationToken)
    {
        var isConnect = stream.Head.Method == "CONNECT";
        if (await RefuseProxyLoginAsync(stream))
        {
            return;
        }

        await (isConnect ? TunnelAsync(stream, cancellationToken) : ForwardAsync(stream, cancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => loginGate.Dispose();

    private static List<HttpRequestField> Http1Fields(Http2RequestHead head) =>
        head.Fields.Select(field => new HttpRequestField(field.Key, field.Value)).ToList();

    private static IEnumerable<HpackHeaderField> RelayedResponseFields(OriginResponseHead response) =>
        ProxyFieldRules.RelayedResponseFields(response.Fields, relaysChunkedBody: false, closesClientConnection: false)
            .Select(field => new HpackHeaderField(field.Name.ToLowerInvariant(), field.Value))
            .Prepend(new HpackHeaderField(":status", response.StatusCode.ToString(CultureInfo.InvariantCulture)));

    private static byte[] Chunk(byte[] piece) =>
        [.. Encoding.ASCII.GetBytes(piece.Length.ToString("x", CultureInfo.InvariantCulture) + "\r\n"), .. piece, (byte)'\r', (byte)'\n'];

    // Judges the stream's proxy login (ADR-0088 decision 5): true when it was refused and answered.
    private async Task<bool> RefuseProxyLoginAsync(Http2RequestStream stream)
    {
        var head = stream.Head;
        var request = new HttpAuthenticationRequest(head.Method, head.Target, head.Method is not ("GET" or "HEAD"), ProxyFieldRules.ProxyLoginFields(Http1Fields(head)));
        HttpAuthenticationVerdict verdict;
        await loginGate.WaitAsync(context.CancellationToken);
        try
        {
            verdict = await authenticationSession.JudgeAsync(request, context.CancellationToken);
        }
        finally
        {
            loginGate.Release();
        }

        if (verdict.CheckedLogin is { } checkedLogin)
        {
            context.Log.Note(checkedLogin.Note);
        }

        return verdict.Outcome switch
        {
            HttpAuthenticationOutcome.Proceed => false,
            HttpAuthenticationOutcome.Forbidden => await AnswerAsync(stream, HttpStatus.Forbidden, "the authentication policy forbade the request"),
            _ => await AnswerAsync(stream, HttpProxyStatus.ProxyAuthenticationRequired, "a proxy login is needed", verdict.WwwAuthenticateValues),
        };
    }

    /// <summary>
    /// Writes an answer the proxy makes itself on the stream, as on HTTP/1.1 but without
    /// <c>Connection</c>: <c>:status</c>, <c>date</c>, <c>server: surl</c>, a
    /// <c>proxy-authenticate</c> per challenge, and the fixed-text body that ends the stream.
    /// </summary>
    private async Task<bool> AnswerAsync(Http2RequestStream stream, HttpStatus status, string why, IReadOnlyList<string>? challenges = null)
    {
        var body = HttpProxyExchange.FixedText(status);
        List<HpackHeaderField> fields =
        [
            new(":status", status.Code.ToString(CultureInfo.InvariantCulture)),
            new("date", context.TimeProvider.GetUtcNow().ToString("R", CultureInfo.InvariantCulture)),
            new("server", HttpResponseHead.ServerName),
            .. (challenges ?? []).Select(challenge => new HpackHeaderField("proxy-authenticate", challenge)),
            new("content-type", "text/plain; charset=us-ascii"),
            new("content-length", body.Length.ToString(CultureInfo.InvariantCulture)),
        ];
        context.Log.Note($"Stream {stream.Id}: {stream.Head.Method} {stream.Head.Target}: answered {status.Code}: {why}.");
        await stream.WriteHeadersAsync(fields, endStream: false);
        await stream.WriteDataAsync(body, endStream: true);

        return true;
    }

    private async Task<OriginLink?> ConnectAsync(Http2RequestStream stream, ProxyTarget target)
    {
        try
        {
            var connection = await originConnector.ConnectAsync(target.Host, target.Port, context.Limits.HeadTimeout, context.CancellationToken);

            return new OriginLink(target.Host, target.Port, connection);
        }
        catch (OriginConnectionException exception)
        {
            var (status, logLine) = HttpProxyStatus.ForOriginFailure(exception.Failure, target.Host, target.Port);
            context.Log.Note(logLine);
            await AnswerAsync(stream, status, $"the origin {target.Host}:{target.Port} could not be used ({exception.Failure})");

            return null;
        }
    }

    private async Task TunnelAsync(Http2RequestStream stream, CancellationToken cancellationToken)
    {
        if (ProxyTarget.FromAuthorityForm(stream.Head.Target) is not { } target)
        {
            await AnswerAsync(stream, HttpStatus.BadRequest, "the CONNECT target is not host:port");

            return;
        }

        if (await ConnectAsync(stream, target) is not { } link)
        {
            return;
        }

        await using (link)
        {
            context.Log.Note($"Stream {stream.Id}: tunnelling to {target.Host}:{target.Port}; answered 200.");
            await stream.WriteHeadersAsync([new HpackHeaderField(":status", "200")], endStream: false);
            var closedCleanly = await Http2ProxyTunnel.RelayAsync(stream, link.Connection, cancellationToken);
            context.Log.Note(closedCleanly
                ? $"Stream {stream.Id}: the tunnel to {target.Host}:{target.Port} closed."
                : $"Stream {stream.Id}: the tunnel to {target.Host}:{target.Port} failed.");
        }
    }

    // :scheme http only (ftp:// over HTTP/2 is not served yet); the target is :authority and :path.
    private (HttpStatus Status, string Why)? JudgeForwardable(Http2RequestHead head, ProxyTarget? target)
    {
        if (head.Scheme != "http")
        {
            return (HttpStatus.NotImplemented, $"the proxy forwards only http:// requests over HTTP/2, not {head.Scheme}://");
        }

        if (target is null)
        {
            return (HttpStatus.BadRequest, "the request's :authority is not host or host:port");
        }

        return IsOverTheUploadLimit(head.ContentLength ?? 0) ? (HttpStatus.ContentTooLarge, "the request body is larger than the upload limit") : null;
    }

    // MaxUploadBytes 0 means no limit.
    private bool IsOverTheUploadLimit(long bytes) => context.Limits.MaxUploadBytes > 0 && bytes > context.Limits.MaxUploadBytes;

    private async Task ForwardAsync(Http2RequestStream stream, CancellationToken cancellationToken)
    {
        var head = stream.Head;
        var target = ProxyTarget.FromAbsoluteForm($"http://{head.Authority}{head.Target}");
        if (JudgeForwardable(head, target) is { } refusal)
        {
            await AnswerAsync(stream, refusal.Status, refusal.Why);

            return;
        }

        if (await ConnectAsync(stream, target!) is not { } link)
        {
            return;
        }

        await using (link)
        {
            await ExchangeWithOriginAsync(stream, target!, link, cancellationToken);
        }
    }

    private async Task ExchangeWithOriginAsync(Http2RequestStream stream, ProxyTarget target, OriginLink link, CancellationToken cancellationToken)
    {
        var head = stream.Head;
        var sendsChunked = stream.IsRequestBodyAnnounced && head.ContentLength is null;
        var fields = ProxyFieldRules.ForwardedRequestFields(Http1Fields(head), target.Authority, HttpProxyExchange.IsMaxForwardsMethod(head.Method), sendsChunked);
        var outcome = await SendRequestAsync(stream, link.Connection, HttpProxyExchange.HeadBytes($"{head.Method} {target.OriginForm} HTTP/1.1", fields), sendsChunked, cancellationToken);
        if (outcome != BodySendOutcome.Sent)
        {
            await AnswerBodyFailureAsync(stream, outcome, target);

            return;
        }

        if (await ReadFinalResponseAsync(stream, link, target) is { } response)
        {
            await RelayResponseAsync(stream, response, link, target, cancellationToken);
        }
    }

    private async Task<BodySendOutcome> SendRequestAsync(Http2RequestStream stream, IConnection origin, byte[] headBytes, bool sendsChunked, CancellationToken cancellationToken)
    {
        try
        {
            await origin.WriteAsync(headBytes, cancellationToken);

            return await SendBodyAsync(stream, origin, sendsChunked, cancellationToken);
        }
        catch (IOException)
        {
            return BodySendOutcome.OriginClosed;
        }
    }

    private async Task<BodySendOutcome> SendBodyAsync(Http2RequestStream stream, IConnection origin, bool sendsChunked, CancellationToken cancellationToken)
    {
        long total = 0;
        while (await stream.ReadBodyAsync(cancellationToken) is { } piece)
        {
            total += piece.Length;
            if (IsOverTheUploadLimit(total))
            {
                return BodySendOutcome.TooLarge;
            }

            await origin.WriteAsync(sendsChunked ? Chunk(piece) : piece, cancellationToken);
        }

        if (stream.HasRequestBodyEndedEarly || stream.HasContentLengthMismatch)
        {
            return stream.HasRequestBodyEndedEarly ? BodySendOutcome.EndedEarly : BodySendOutcome.ContentLengthMismatch;
        }

        if (sendsChunked)
        {
            await origin.WriteAsync("0\r\n\r\n"u8.ToArray(), cancellationToken);
        }

        return BodySendOutcome.Sent;
    }

    // Answers every outcome but Sent, which never reaches here; the last is EndedEarly.
    private async Task AnswerBodyFailureAsync(Http2RequestStream stream, BodySendOutcome outcome, ProxyTarget target)
    {
        if (outcome == BodySendOutcome.TooLarge)
        {
            await AnswerAsync(stream, HttpStatus.ContentTooLarge, "the request body is larger than the upload limit");

            return;
        }

        if (outcome == BodySendOutcome.ContentLengthMismatch)
        {
            context.Log.Note($"Stream {stream.Id}: the request body did not add up to its content-length; reset PROTOCOL_ERROR.");
            await stream.ResetAsync(Http2ErrorCode.ProtocolError, "the request body did not add up to its content-length");

            return;
        }

        if (outcome == BodySendOutcome.OriginClosed)
        {
            await AnswerAsync(stream, HttpProxyStatus.BadGateway, $"origin {target.Host}:{target.Port} closed before the request was sent");

            return;
        }

        context.Log.Note($"Stream {stream.Id}: curl ended the stream before its request body ended.");
    }

    // Reads heads until a final one, relaying each interim 1xx as a header block; null when it
    // answered a failure instead: 504 for a timeout, 502 for anything unreadable or a 101, which
    // HTTP/2 cannot carry (RFC 9113 section 8.6).
    private async Task<OriginResponseHead?> ReadFinalResponseAsync(Http2RequestStream stream, OriginLink link, ProxyTarget target)
    {
        while (true)
        {
            var (response, failure) = await ReadResponseHeadAsync(link, target);
            if (failure is { } answer)
            {
                await AnswerAsync(stream, answer.Status, answer.Why);

                return null;
            }

            if (response!.StatusCode == 101)
            {
                await AnswerAsync(stream, HttpProxyStatus.BadGateway, $"origin {target.Host}:{target.Port} answered 101, which HTTP/2 cannot relay");

                return null;
            }

            if (response.StatusCode >= 200)
            {
                return response;
            }

            await stream.WriteHeadersAsync(RelayedResponseFields(response), endStream: false);
        }
    }

    private async Task<(OriginResponseHead? Head, (HttpStatus Status, string Why)? Failure)> ReadResponseHeadAsync(OriginLink link, ProxyTarget target)
    {
        using var headTimeout = new CancellationTokenSource(HttpProxyExchange.TimerDelay(context.Limits.HeadTimeout), context.TimeProvider);
        using var headTimeoutOrExchange = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headTimeout.Token);
        try
        {
            var (head, _) = await link.ReadResponseHeadAsync(context.Limits.MaxRequestHeadBytes, headTimeoutOrExchange.Token);

            return head is not null ? (head, null) : (null, (HttpProxyStatus.BadGateway, $"origin {target.Host}:{target.Port} sent no response the proxy could read"));
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            return (null, (HttpProxyStatus.GatewayTimeout, $"origin {target.Host}:{target.Port} did not answer in time"));
        }
        catch (IOException)
        {
            return (null, (HttpProxyStatus.BadGateway, $"origin {target.Host}:{target.Port} failed before it answered"));
        }
    }

    private async Task RelayResponseAsync(Http2RequestStream stream, OriginResponseHead response, OriginLink link, ProxyTarget target, CancellationToken cancellationToken)
    {
        var body = OriginResponseBody.Of(stream.Head.Method, response);
        if (body.Kind == OriginResponseBodyKind.Invalid)
        {
            await AnswerAsync(stream, HttpProxyStatus.BadGateway, $"origin {target.Host}:{target.Port} sent a response whose length cannot be read");

            return;
        }

        var hasNoBody = body is { Kind: OriginResponseBodyKind.None } or { Kind: OriginResponseBodyKind.Length, Length: 0 };
        context.Log.Note($"Stream {stream.Id}: forwarded {stream.Head.Method} to {target.Host}:{target.Port}; the origin answered {response.StatusCode}.");
        await stream.WriteHeadersAsync(RelayedResponseFields(response), endStream: hasNoBody);
        if (hasNoBody)
        {
            return;
        }

        if (await RelayBodyAsync(stream, body, link, cancellationToken))
        {
            await stream.WriteDataAsync(ReadOnlyMemory<byte>.Empty, endStream: true);

            return;
        }

        context.Log.Note($"Stream {stream.Id}: the origin's response body ended early or was malformed; reset INTERNAL_ERROR.");
        await stream.ResetAsync(Http2ErrorCode.InternalError, "the origin's response body ended early or was malformed");
    }

    // False when the origin's body ended early, was malformed, or its connection failed.
    private static async Task<bool> RelayBodyAsync(Http2RequestStream stream, OriginResponseBody body, OriginLink link, CancellationToken cancellationToken)
    {
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write = (bytes, _) => new ValueTask(stream.WriteDataAsync(bytes, endStream: false));
        try
        {
            // None and Invalid never reach here, so what is neither Length nor Chunked is close-delimited.
            if (body.Kind == OriginResponseBodyKind.Length)
            {
                return await ProxyBodyCopier.CopyLengthAsync(link.Reader, write, body.Length, cancellationToken);
            }

            if (body.Kind == OriginResponseBodyKind.Chunked)
            {
                return await ProxyBodyCopier.CopyChunkedDataAsync(link.Reader, write, cancellationToken) == ChunkedCopyOutcome.Copied;
            }

            return await CopyUntilClosedAsync(link, write, cancellationToken);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static async Task<bool> CopyUntilClosedAsync(OriginLink link, Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write, CancellationToken cancellationToken)
    {
        await ProxyBodyCopier.CopyUntilClosedAsync(link.Reader, write, cancellationToken);

        return true;
    }
}
