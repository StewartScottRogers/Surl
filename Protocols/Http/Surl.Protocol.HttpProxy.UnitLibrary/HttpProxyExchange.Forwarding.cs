using System.Globalization;
using System.Text;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// Forwarding an absolute-form request (ADR-0088 decision 3): the target, the request sent to
/// the origin, its body, the origin's interim and final responses, and the response relayed.
/// </summary>
internal sealed partial class HttpProxyExchange
{
    private const string AllowedMethods = "CONNECT, DELETE, GET, HEAD, OPTIONS, PATCH, POST, PUT, TRACE";

    private static readonly TimeSpan MaxTimerDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1.0);

    private static readonly HashSet<string> IdempotentMethods = ["GET", "HEAD", "OPTIONS", "TRACE", "PUT", "DELETE"];

    private enum ReplyFailure
    {
        None,
        OriginClosedEarly,
        BadResponse,
        TimedOut,
        ClientBodyMalformed,
        ClientBodyTooLarge,
        ClientBodyEnded,
    }

    private static bool IsMaxForwardsMethod(string method) => method is "OPTIONS" or "TRACE";

    private static (HttpStatus Status, string Why, bool MustClose)? JudgeTarget(ProxyTarget? target) =>
        target is null
            ? (HttpStatus.BadRequest, "the request target is not absolute-form", true)
            : target.Scheme == "http" ? null : (HttpStatus.NotImplemented, $"the proxy does not forward {target.Scheme}:// requests", false);

    private static (HttpStatus Status, string Why, bool MustClose)? JudgeBody(HttpRequestBodyFraming framing, long maxUploadBytes) => framing.Kind switch
    {
        HttpRequestBodyFramingKind.Unreadable => (HttpStatus.NotImplemented, "the request's Transfer-Encoding is not chunked alone", true),
        HttpRequestBodyFramingKind.InvalidContentLength => (HttpStatus.BadRequest, "the request's Content-Length is not one number", true),
        HttpRequestBodyFramingKind.ContentLength when maxUploadBytes > 0 && framing.ContentLength > maxUploadBytes => (HttpStatus.ContentTooLarge, "the request body is larger than the upload limit", true),
        _ => null,
    };

    private static byte[] ForwardedHeadBytes(HttpRequestHead head, ProxyTarget target, HttpRequestBodyFraming framing)
    {
        var fields = ProxyFieldRules.ForwardedRequestFields(head.Fields, target.Authority, IsMaxForwardsMethod(head.Method), framing.Kind == HttpRequestBodyFramingKind.Chunked);

        return HeadBytes($"{head.Method} {target.OriginForm} HTTP/1.1", fields);
    }

    private static byte[] HeadBytes(string startLine, IEnumerable<HttpRequestField> fields)
    {
        var text = new StringBuilder(startLine).Append("\r\n");
        foreach (var field in fields)
        {
            text.Append(field.Name).Append(": ").Append(field.Value).Append("\r\n");
        }

        return Encoding.Latin1.GetBytes(text.Append("\r\n").ToString());
    }

    private static TimeSpan TimerDelay(TimeSpan headTimeout) =>
        headTimeout > MaxTimerDelay ? Timeout.InfiniteTimeSpan : headTimeout;

    private async Task<bool> AnswerForwardableAsync(HttpRequestHead head)
    {
        var target = ProxyTarget.FromAbsoluteForm(head.RequestTarget);
        var framing = HttpRequestBodyFraming.Of(head);
        var endsConnection = RequestEndsConnection(head) || framing.Kind != HttpRequestBodyFramingKind.None;
        if ((JudgeTarget(target) ?? JudgeBody(framing, context.Limits.MaxUploadBytes)) is { } refusal)
        {
            return await AnswerAsync(refusal.Status, refusal.Why, refusal.MustClose || endsConnection);
        }

        if (IsMaxForwardsMethod(head.Method) && ProxyFieldRules.MaxForwards(head.Fields) == 0)
        {
            return await AnswerHereAsync(head, endsConnection);
        }

        try
        {
            return await ForwardAsync(head, target!, framing);
        }
        catch (OriginConnectionException exception)
        {
            return await AnswerOriginFailureAsync(exception.Failure, target!, endsConnection);
        }
    }

    // Max-Forwards: 0 on OPTIONS or TRACE: the proxy is the last recipient (RFC 9110 section 7.6.2).
    private async Task<bool> AnswerHereAsync(HttpRequestHead head, bool closes)
    {
        var isTrace = head.Method == "TRACE";
        var body = isTrace ? HeadBytes($"{head.Method} {head.RequestTarget} HTTP/1.{head.Version.Minor}", head.Fields) : [];
        var answer = NewHead(HttpStatus.Ok)
            .AddField("Allow", isTrace ? null : AllowedMethods)
            .AddField("Content-Type", isTrace ? "message/http" : null)
            .AddField("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture))
            .AddField("Connection", closes ? "close" : null);

        return await WriteAnswerAsync(answer, body, $"Answered {head.Method} with Max-Forwards: 0 here; answered 200.", closes);
    }

    private async Task<bool> ForwardAsync(HttpRequestHead head, ProxyTarget target, HttpRequestBodyFraming framing)
    {
        var (reply, reused) = await SendToOriginAsync(head, target, framing);
        if (reply.Failure == ReplyFailure.OriginClosedEarly && reused && framing.Kind == HttpRequestBodyFramingKind.None && IdempotentMethods.Contains(head.Method))
        {
            context.Log.Note($"Origin {target.Host}:{target.Port} closed the kept connection; reconnecting once.");
            await DropOriginAsync();
            (reply, _) = await SendToOriginAsync(head, target, framing);
        }

        return reply.Head is { } response
            ? await RelayResponseAsync(head, target, response, reply.BodyUnsent)
            : await AnswerReplyFailureAsync(reply.Failure, target, RequestEndsConnection(head) || framing.Kind != HttpRequestBodyFramingKind.None);
    }

    private async Task<(OriginReply Reply, bool Reused)> SendToOriginAsync(HttpRequestHead head, ProxyTarget target, HttpRequestBodyFraming framing)
    {
        var reused = origin is { } kept && kept.GoesTo(target.Host, target.Port);
        if (!reused)
        {
            await DropOriginAsync();
            origin = await ConnectAsync(target);
        }

        try
        {
            await origin!.Connection.WriteAsync(ForwardedHeadBytes(head, target, framing), Cancellation);

            return (await SendBodyAndReadReplyAsync(head, origin, framing), reused);
        }
        catch (IOException)
        {
            return (OriginReply.Failed(ReplyFailure.OriginClosedEarly), reused);
        }
    }

    private async Task<OriginReply> SendBodyAndReadReplyAsync(HttpRequestHead head, OriginLink link, HttpRequestBodyFraming framing)
    {
        var expectsContinue = framing.Kind != HttpRequestBodyFramingKind.None && ProxyFieldRules.ListsToken(head.Fields, "Expect", "100-continue");
        if (expectsContinue)
        {
            var first = await ReadReplyAsync(link, stopsAtContinue: true);
            if (first.Head is not { StatusCode: 100 })
            {
                return first with { BodyUnsent = true };
            }
        }

        var bodyFailure = await SendBodyAsync(link, framing);

        return bodyFailure == ReplyFailure.None ? await ReadReplyAsync(link, stopsAtContinue: false) : OriginReply.Failed(bodyFailure);
    }

    private async Task<ReplyFailure> SendBodyAsync(OriginLink link, HttpRequestBodyFraming framing)
    {
        if (framing.Kind == HttpRequestBodyFramingKind.ContentLength)
        {
            return await ProxyBodyCopier.CopyLengthAsync(reader, link.Connection, framing.ContentLength, Cancellation) ? ReplyFailure.None : ReplyFailure.ClientBodyEnded;
        }

        var outcome = framing.Kind == HttpRequestBodyFramingKind.Chunked
            ? await ProxyBodyCopier.CopyChunkedAsync(reader, link.Connection, context.Limits.MaxUploadBytes, Cancellation)
            : ChunkedCopyOutcome.Copied;

        return outcome switch
        {
            ChunkedCopyOutcome.Copied => ReplyFailure.None,
            ChunkedCopyOutcome.TooLarge => ReplyFailure.ClientBodyTooLarge,
            _ => ReplyFailure.ClientBodyMalformed,
        };
    }

    // Reads heads until a final one (or a 101), relaying each other 1xx to curl; with
    // stopsAtContinue, a 100 is relayed and returned so the body can follow it.
    private async Task<OriginReply> ReadReplyAsync(OriginLink link, bool stopsAtContinue)
    {
        while (true)
        {
            var reply = await ReadReplyHeadAsync(link);
            if (reply.Head is not { StatusCode: < 200 and not 101 } interim)
            {
                return reply;
            }

            await client.WriteAsync(interim.ToRelayedBytes(ProxyFieldRules.RelayedResponseFields(interim.Fields, false, false)), Cancellation);
            if (stopsAtContinue && interim.StatusCode == 100)
            {
                return reply;
            }
        }
    }

    private async Task<OriginReply> ReadReplyHeadAsync(OriginLink link)
    {
        using var headTimeout = new CancellationTokenSource(TimerDelay(context.Limits.HeadTimeout), context.TimeProvider);
        using var headTimeoutOrExchange = CancellationTokenSource.CreateLinkedTokenSource(Cancellation, headTimeout.Token);
        try
        {
            var (head, closedBeforeAnyByte) = await link.ReadResponseHeadAsync(context.Limits.MaxRequestHeadBytes, headTimeoutOrExchange.Token);

            return head is not null
                ? new OriginReply(head, ReplyFailure.None, false)
                : OriginReply.Failed(closedBeforeAnyByte ? ReplyFailure.OriginClosedEarly : ReplyFailure.BadResponse);
        }
        catch (OperationCanceledException) when (!Cancellation.IsCancellationRequested)
        {
            return OriginReply.Failed(ReplyFailure.TimedOut);
        }
    }

    private async Task<bool> AnswerReplyFailureAsync(ReplyFailure failure, ProxyTarget target, bool closes)
    {
        await DropOriginAsync();
        var where = $"{target.Host}:{target.Port}";
        var (status, why, mustClose) = failure switch
        {
            ReplyFailure.TimedOut => (HttpProxyStatus.GatewayTimeout, $"origin {where} did not answer in time", false),
            ReplyFailure.ClientBodyMalformed => (HttpStatus.BadRequest, "the request's chunked body is malformed", true),
            ReplyFailure.ClientBodyTooLarge => (HttpStatus.ContentTooLarge, "the request body is larger than the upload limit", true),
            ReplyFailure.ClientBodyEnded => (HttpStatus.BadRequest, "the client closed before its request body ended", true),
            _ => (HttpProxyStatus.BadGateway, $"origin {where} sent no response the proxy could read ({failure})", false),
        };

        return await AnswerAsync(status, why, mustClose || closes);
    }

    private async Task<bool> RelayResponseAsync(HttpRequestHead head, ProxyTarget target, OriginResponseHead response, bool bodyUnsent)
    {
        var body = OriginResponseBody.Of(head.Method, response);
        if (body.Kind == OriginResponseBodyKind.Invalid)
        {
            return await AnswerReplyFailureAsync(ReplyFailure.BadResponse, target, RequestEndsConnection(head) || bodyUnsent);
        }

        var isUpgrade = response.StatusCode == 101;
        var closesClient = isUpgrade || bodyUnsent || body.Kind == OriginResponseBodyKind.UntilClose || RequestEndsConnection(head);
        var fields = ProxyFieldRules.RelayedResponseFields(response.Fields, body.Kind == OriginResponseBodyKind.Chunked, closesClient && !isUpgrade);
        context.Log.Note($"Forwarded {head.Method} to {target.Host}:{target.Port}; the origin answered {response.StatusCode}.");
        await client.WriteAsync(response.ToRelayedBytes(fields), Cancellation);

        return isUpgrade
            ? await RelayUpgradedAsync(target)
            : await RelayResponseBodyAsync(body, response.KeepsConnection && body.Kind != OriginResponseBodyKind.UntilClose, closesClient);
    }

    private async Task<bool> RelayUpgradedAsync(ProxyTarget target)
    {
        var closedCleanly = await ProxyTunnel.RelayAsync(client, reader, origin!, Cancellation);
        context.Log.Note(closedCleanly
            ? $"The upgraded connection to {target.Host}:{target.Port} closed."
            : $"The upgraded connection to {target.Host}:{target.Port} failed; both connections were aborted.");
        await DropOriginAsync();

        return false;
    }

    private async Task<bool> RelayResponseBodyAsync(OriginResponseBody body, bool keepsOrigin, bool closesClient)
    {
        var relayed = await RelayBodyBytesAsync(body, origin!);
        if (!relayed)
        {
            context.Log.Note("The origin's response body ended early or was malformed; both connections closed.");
            client.Abort();
        }

        if (!relayed || !keepsOrigin)
        {
            await DropOriginAsync();
        }

        if (relayed && closesClient)
        {
            await client.CompleteWritesAsync(Cancellation);
        }

        return relayed && !closesClient;
    }

    // False when the origin's body ended early or was malformed; a body with no framing to
    // read (none) is relayed by relaying nothing.
    private async Task<bool> RelayBodyBytesAsync(OriginResponseBody body, OriginLink link)
    {
        if (body.Kind == OriginResponseBodyKind.Length)
        {
            return await ProxyBodyCopier.CopyLengthAsync(link.Reader, client, body.Length, Cancellation);
        }

        if (body.Kind == OriginResponseBodyKind.Chunked)
        {
            return await ProxyBodyCopier.CopyChunkedAsync(link.Reader, client, 0, Cancellation) == ChunkedCopyOutcome.Copied;
        }

        if (body.Kind == OriginResponseBodyKind.UntilClose)
        {
            await ProxyBodyCopier.CopyUntilClosedAsync(link.Reader, client, Cancellation);
        }

        return true;
    }

    private sealed record OriginReply(OriginResponseHead? Head, ReplyFailure Failure, bool BodyUnsent)
    {
        public static OriginReply Failed(ReplyFailure failure) => new(null, failure, false);
    }
}
