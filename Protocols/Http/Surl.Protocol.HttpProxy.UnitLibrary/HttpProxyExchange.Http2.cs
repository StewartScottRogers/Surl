using Surl.Http2;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// The HTTPS proxy over HTTP/2 (ADR-0088 decision 6 as Amendment 5 amends it): a TLS connection
/// whose handshake agreed ALPN <c>h2</c>, what curl's <c>--proxy-http2</c> asks for, is served
/// by <see cref="Http2ConnectionServer"/>, each stream answered by
/// <see cref="Http2ProxyStreamHandler"/>.
/// </summary>
internal sealed partial class HttpProxyExchange
{
    /// <summary>The ALPN protocol ID of HTTP/2 over TLS (RFC 9113 section 3.2).</summary>
    internal const string Http2ApplicationProtocol = "h2";

    private async Task ServeHttp2Async(TlsSession tls)
    {
        context.Log.Note($"HTTPS proxy: curl's connection to the proxy is TLS ({tls.Protocol}, ALPN h2); requests are read as HTTP/2 streams.");
        var preface = new Http2ConnectionPreface();
        if (!await ReadPrefaceWithinHeadTimeoutAsync(preface))
        {
            return;
        }

        if (!preface.IsComplete)
        {
            context.Log.Note($"The {preface.BytesRead.Length} bytes read are not the HTTP/2 connection preface ALPN h2 promised; the connection closes.");

            return;
        }

        using var handler = new Http2ProxyStreamHandler(context, originConnector, authenticationSession);
        await new Http2ConnectionServer(handler, context, new AlreadyReadBytesConnection(client, preface.BytesReadAfterPreface)).ServeAsync();
    }

    // The head timeout bounds the preface as it bounds a first request head: when it runs out
    // the connection closes with no bytes written. False when it ran out.
    private async Task<bool> ReadPrefaceWithinHeadTimeoutAsync(Http2ConnectionPreface preface)
    {
        using var headTimeout = new CancellationTokenSource(TimerDelay(context.Limits.HeadTimeout), context.TimeProvider);
        using var headTimeoutOrExchange = CancellationTokenSource.CreateLinkedTokenSource(Cancellation, headTimeout.Token);
        try
        {
            await preface.ReadAsync(client, headTimeoutOrExchange.Token);

            return true;
        }
        catch (OperationCanceledException) when (!Cancellation.IsCancellationRequested)
        {
            context.Log.Note($"The HTTP/2 connection preface was not complete within the head timeout ({preface.BytesRead.Length} bytes read); the connection closes.");

            return false;
        }
    }
}
