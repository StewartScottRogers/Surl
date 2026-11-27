using System.Security.Cryptography;
using Surl.Content;
using Surl.Cookies;
using Surl.Http3;
using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Protocol.Http;

/// <summary>
/// Serves HTTP/3 over QUIC (RFC 9114, RFC 9000) on the UDP side of an <c>https</c> listen URL
/// (ADR-0082, decisions 5 and 6): each datagram flow is one QUIC connection, run by a
/// <see cref="QuicDatagramFlowServer"/> offering <c>h3</c> with the server certificate, whose
/// requests are answered from the content store as the HTTP server answers them over TCP.
/// </summary>
/// <remarks>
/// The serving engine registers it beside the <c>https</c> connection server, so a listen URL
/// binds both on one port. QUIC's <c>max_idle_timeout</c> is the <c>--idle-timeout</c> it is
/// given, and the TLS handshake must complete within the exchange's head timeout (ADR-0082,
/// decision 7). A flow past a connection limit whose first datagram is a version 1 Initial is refused
/// with one Initial packet holding <c>CONNECTION_CLOSE</c> <c>CONNECTION_REFUSED</c> (0x02); any other
/// first datagram gets no bytes (<see cref="QuicConnectionRefusal"/>).
/// Every reply gains a <c>set-cookie</c> field for each configured cookie it is scoped to, and each cookie a request carries
/// is published as a <see cref="CookieReceivedEvent"/>, whether or not any is set (ADR-0189).
/// A flow a <c>close=graceful</c> drop fault cuts (<see cref="IGracefullyCutDatagramFlow"/>) ends with the QUIC
/// connection closed by <c>CONNECTION_CLOSE</c> <c>NO_ERROR</c>, with no <c>GOAWAY</c> (ADR-0188, amendment 5).
/// </remarks>
public sealed class Http3ProtocolServer : IGracefulCutDatagramProtocolServer, IDatagramRefusalWriter, IExchangeBytesRedactingServer
{
    private readonly ContentStore contentStore;
    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly IReadOnlyList<ReadOnlyMemory<byte>> certificateChain;
    private readonly AsymmetricAlgorithm certificateKey;
    private readonly TimeSpan idleTimeout;
    private readonly Func<IReadOnlyList<ReadOnlyMemory<byte>>, bool>? trustsClientCertificateChain;
    private readonly IReadOnlyList<CookieDefinition> cookies;

    /// <summary>
    /// Creates the server.
    /// </summary>
    /// <param name="contentStore">What GET and HEAD serve.</param>
    /// <param name="authenticationPolicy">The policy every request is judged by, as over an encrypted connection.</param>
    /// <param name="certificateChain">The server certificate and its intermediates, DER, leaf first.</param>
    /// <param name="certificateKey">The certificate's private key: RSA, or ECDSA on P-256, P-384 or P-521.</param>
    /// <param name="idleTimeout">QUIC's <c>max_idle_timeout</c>; zero for none.</param>
    /// <param name="trustsClientCertificateChain">
    /// Decides whether a client's certificate chain (DER, end-entity certificate first) chains to the
    /// <c>--cacert</c> anchors (ADR-0010 section 5). When given, every QUIC handshake asks for a
    /// client certificate and completes only with one it trusts, and the TLS session each request is
    /// judged with carries that certificate. Null asks for none.
    /// </param>
    /// <param name="cookies">The cookies to set, in the order written; null or none leaves every reply unchanged.</param>
    public Http3ProtocolServer(
        ContentStore contentStore,
        IAuthenticationPolicy authenticationPolicy,
        IReadOnlyList<ReadOnlyMemory<byte>> certificateChain,
        AsymmetricAlgorithm certificateKey,
        TimeSpan idleTimeout,
        Func<IReadOnlyList<ReadOnlyMemory<byte>>, bool>? trustsClientCertificateChain = null,
        IReadOnlyList<CookieDefinition>? cookies = null)
    {
        ArgumentNullException.ThrowIfNull(contentStore);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);
        ArgumentNullException.ThrowIfNull(certificateChain);
        ArgumentNullException.ThrowIfNull(certificateKey);

        this.contentStore = contentStore;
        this.authenticationPolicy = authenticationPolicy;
        this.certificateChain = certificateChain;
        this.certificateKey = certificateKey;
        this.idleTimeout = idleTimeout;
        this.trustsClientCertificateChain = trustsClientCertificateChain;
        this.cookies = cookies ?? [];
    }

    /// <summary>
    /// Creates the redactor of the decrypted-bytes notes of one HTTP/3 connection: it withholds the
    /// <c>authorization</c> and <c>proxy-authorization</c> values of request header blocks (ADR-0182, BL-1424).
    /// </summary>
    /// <param name="context">The exchange the redactor is for.</param>
    /// <returns>A redactor for that exchange alone.</returns>
    public IReceivedBytesRedactor CreateRedactor(ExchangeRedactionContext context) => new Http3RequestBytesRedactor();

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["https"]);

    /// <summary>Whether every QUIC handshake requires a client certificate, as it does when the server was given a trust check.</summary>
    public bool RequiresClientCertificate => trustsClientCertificateChain is not null;

    /// <summary>
    /// Runs one QUIC connection on <paramref name="flow"/> and answers its HTTP/3 requests until
    /// it ends or the exchange is cancelled.
    /// </summary>
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
            IdleTimeout = idleTimeout,
            HandshakeTimeout = headTimeout == Timeout.InfiniteTimeSpan ? null : headTimeout,
            TrustsClientCertificateChain = trustsClientCertificateChain,
            DecryptedBytes = context.DecryptedBytes,
        };
        using var random = RandomNumberGenerator.Create();
        var application = new Http3ApplicationProtocol(contentStore, authenticationPolicy, cookies, context);

        // Once HTTP/3 is served, cancelling the exchange is the HTTP/3 server's to answer, with
        // GOAWAY and CONNECTION_CLOSE H3_NO_ERROR, and the flow loop ends after it (ADR-0082
        // Amendment 1); before then nothing is served, and the flow loop is stopped at once.
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

    /// <summary>
    /// Refuses a flow past a connection limit (ADR-0082 decision 7, "Too many connections"): a first
    /// datagram that is a version 1 Initial gets one Initial packet holding <c>CONNECTION_CLOSE</c>
    /// <c>CONNECTION_REFUSED</c>; any other gets no bytes. Both limits are refused alike.
    /// </summary>
    /// <param name="flow">The flow past the limit.</param>
    /// <param name="refusal">Which limit it is past.</param>
    /// <param name="cancellationToken">Cancelled when the engine gives up on the refusal.</param>
    /// <returns>A task that completes when the refusal is sent, or at once when nothing is.</returns>
    public async ValueTask WriteRefusalAsync(IDatagramFlow flow, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(flow);

        using var random = RandomNumberGenerator.Create();
        if (QuicConnectionRefusal.Write(flow.FirstDatagram, random) is { } datagram)
        {
            await flow.SendAsync(datagram, cancellationToken);
        }
    }
}
