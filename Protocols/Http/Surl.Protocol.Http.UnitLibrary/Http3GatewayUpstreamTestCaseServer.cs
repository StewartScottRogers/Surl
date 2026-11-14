using System.Security.Cryptography;
using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Protocol.Http;

/// <summary>
/// The <c>http/3</c> server half of upstream test cases (ADR-0154): what upstream's nghttpx is in
/// front of the case's <c>http</c> half on <c>%HTTP3PORT</c>. Each datagram flow is one QUIC
/// connection offering <c>h3</c> with the given certificate; every request stream is forwarded to the
/// case's <c>http</c> half as HTTP/1.1 in absolute form (<see cref="NghttpxGatewayRequestTranslator"/>)
/// over a fresh connection from the injected <see cref="IOriginConnector"/>, and answered with the
/// reply as nghttpx builds it (<see cref="NghttpxGatewayResponseTranslator"/>), which adds no
/// <c>server</c> field.
/// </summary>
/// <remarks>
/// <para>
/// The stream ends when the reply's <c>Content-Length</c> or last chunk has arrived, or when the
/// backend closes. A backend that sends no complete reply head is answered <c>502 Bad Gateway</c>
/// (as is one that cannot be reached); one that closes in the middle of a body has the stream reset
/// with <c>H3_INTERNAL_ERROR</c>. The connection stays open for the next stream either way.
/// </para>
/// <para>
/// Never composed by the shipped <c>surl</c> (ADR-0080 decision 2): it uses no store, publishes
/// nothing on an event stream and has no fault rules.
/// </para>
/// </remarks>
public sealed class Http3GatewayUpstreamTestCaseServer : IDatagramProtocolServer
{
    private readonly IOriginConnector backendConnector;
    private readonly int backendPort;
    private readonly IReadOnlyList<ReadOnlyMemory<byte>> certificateChain;
    private readonly AsymmetricAlgorithm certificateKey;

    /// <summary>Creates the server.</summary>
    /// <param name="backendConnector">Opens each stream's connection to the backend; loopback-only in a conformance run.</param>
    /// <param name="backendPort">The port of the case's <c>http</c> half on <c>127.0.0.1</c>.</param>
    /// <param name="certificateChain">The server certificate and its intermediates, DER, leaf first.</param>
    /// <param name="certificateKey">The certificate's private key: RSA, or ECDSA on P-256, P-384 or P-521.</param>
    public Http3GatewayUpstreamTestCaseServer(
        IOriginConnector backendConnector,
        int backendPort,
        IReadOnlyList<ReadOnlyMemory<byte>> certificateChain,
        AsymmetricAlgorithm certificateKey)
    {
        ArgumentNullException.ThrowIfNull(backendConnector);
        ArgumentNullException.ThrowIfNull(certificateChain);
        ArgumentNullException.ThrowIfNull(certificateKey);

        this.backendConnector = backendConnector;
        this.backendPort = backendPort;
        this.certificateChain = certificateChain;
        this.certificateKey = certificateKey;
    }

    /// <summary>The one scheme answered: <c>https</c>, the UDP side of its port.</summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["https"]);

    /// <summary>Runs one QUIC connection on <paramref name="flow"/> until it ends or the exchange is cancelled.</summary>
    /// <param name="flow">The client's datagrams, one remote endpoint.</param>
    /// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
    /// <returns>A task that completes once the connection has ended.</returns>
    public async Task ServeAsync(IDatagramFlow flow, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(context);

        var headTimeout = HeadTimeoutDelay.For(context.Limits.HeadTimeout);
        var options = new QuicServerConnectionOptions
        {
            CertificateChain = certificateChain,
            CertificateKey = certificateKey,
            HandshakeTimeout = headTimeout == Timeout.InfiniteTimeSpan ? null : headTimeout,
        };
        using var random = RandomNumberGenerator.Create();
        var application = new Http3GatewayApplicationProtocol(new NghttpxGatewayBackend(backendConnector, backendPort), context);

        // As Http3ProtocolServer does: once HTTP/3 is served, cancelling the exchange is the HTTP/3 server's to answer.
        using var stopBeforeServing = new CancellationTokenSource();
        await using var registration = context.CancellationToken.Register(() =>
        {
            if (!application.HasStarted)
            {
                stopBeforeServing.Cancel();
            }
        });
        await new QuicDatagramFlowServer(options, application, context.TimeProvider, random).ServeAsync(flow, stopBeforeServing.Token);
    }
}
