using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// The <c>http/2</c> server half of upstream test cases (ADR-0154): what upstream's nghttpx is in
/// front of the case's <c>http</c> half. It serves HTTP/2 on a connection the engine has already
/// secured with ALPN <c>h2</c>, or on a plaintext one with prior knowledge, forwards each stream's
/// request to the case's <c>http</c> half as HTTP/1.1 (<see cref="NghttpxGatewayRequestTranslator"/>)
/// over a fresh connection from the injected <see cref="IOriginConnector"/>, and answers the stream
/// with the reply as nghttpx builds it (<see cref="NghttpxGatewayResponseTranslator"/>). On a plaintext\r\n/// connection that does not open with the preface it answers HTTP/1.1 as <see cref="Http2GatewayCleartextUpgrade"/>\r\n/// says: an <c>Upgrade: h2c</c> request becomes stream 1, any other is forwarded as HTTP/1.1.\r\n/// </summary>
/// <remarks>
/// <para>
/// The stream ends when the reply's <c>Content-Length</c> or last chunk has arrived, or when the
/// backend closes. A backend that sends no complete reply head is answered <c>502 Bad Gateway</c>
/// (as is one that cannot be reached); one that closes in the middle of a body has the stream reset
/// with <c>INTERNAL_ERROR</c>. The connection stays open for the next stream either way.
/// </para>
/// <para>
/// Never composed by the shipped <c>surl</c> (ADR-0080 decision 2): it uses no store, publishes
/// nothing on an event stream and has no fault rules.
/// </para>
/// </remarks>
/// <param name="backendConnector">Opens each stream's connection to the backend; loopback-only in a conformance run.</param>
/// <param name="backendPort">The port of the case's <c>http</c> half on <c>127.0.0.1</c>.</param>
public sealed class Http2GatewayUpstreamTestCaseServer(IOriginConnector backendConnector, int backendPort) : IConnectionProtocolServer
{
    private readonly IOriginConnector backendConnector = backendConnector ?? throw new ArgumentNullException(nameof(backendConnector));

    /// <summary>The one scheme answered: <c>http</c>; <c>https</c> is the same server on a connection the engine has secured.</summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["http"]);

    /// <summary>Serves HTTP/2 on <paramref name="connection"/> until the client or the exchange ends it.</summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var preface = new Http2ConnectionPreface();
        await preface.ReadAsync(connection, context.CancellationToken);
        NghttpxGatewayBackend backend = new(backendConnector, backendPort);
        if (preface.IsComplete)
        {
            await new Http2ConnectionServer(
                new Http2GatewayStreamForwarder(backend, upgradedBody: null),
                context,
                new AlreadyReadBytesConnection(connection, preface.BytesReadAfterPreface)).ServeAsync();

            return;
        }

        // Upgrade: h2c and plain HTTP/1.1 are for the plaintext port; a TLS connection speaks h2 or is closed.
        if (connection.TlsSession is not null)
        {
            context.Log.Note("The client did not send the HTTP/2 connection preface; closed with no bytes.");

            return;
        }

        await new Http1ConnectionServer(new Http2GatewayCleartextUpgrade(backend, context), context)
            .ServeAsync(new AlreadyReadBytesConnection(connection, preface.BytesRead));
    }
}
