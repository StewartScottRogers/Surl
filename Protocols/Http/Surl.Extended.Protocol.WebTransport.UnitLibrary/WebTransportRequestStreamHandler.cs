using Surl.Http3;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// Answers every request that is not a WebTransport extended <c>CONNECT</c> (ADR-0117 decision 1):
/// <c>:status 405</c>, <c>allow: CONNECT</c>, no body, FIN - the listener serves no HTTP resource.
/// The stream is recorded as no session, so streams naming it are refused (WTH3 4.6).
/// </summary>
/// <param name="directory">The connection's sessions.</param>
internal sealed class WebTransportRequestStreamHandler(WebTransportSessionDirectory directory) : IHttp3RequestStreamHandler
{
    /// <inheritdoc />
    public async Task ServeRequestStreamAsync(Http3RequestStream stream, CancellationToken cancellationToken)
    {
        directory.Settle(stream.Id, null);
        await stream.WriteHeadersAsync(WebTransportConnectHandler.AnswerFields(WebTransportConnectJudgement.MethodNotAllowed), endStream: true, cancellationToken);
    }
}
