using System.Security.Cryptography;
using Surl.Http3;
using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// Serves WebTransport over HTTP/3 on the UDP port of a <c>webtransport</c> listen URL (ADR-0117;
/// BL-461): each datagram flow is one QUIC connection, run by a <see cref="QuicDatagramFlowServer"/>
/// offering <c>h3</c> with the server certificate and handing the connection to a
/// <see cref="WebTransportApplicationProtocol"/>.
/// </summary>
/// <remarks>
/// A listen URL has no path to name one (ADR-0120 amendment 2), so a session is accepted on every
/// path. QUIC's <c>max_idle_timeout</c> is the <c>--idle-timeout</c> it is given, and the TLS handshake
/// must complete within the exchange's head timeout. A flow past a connection limit is refused as
/// <see cref="QuicConnectionRefusal"/> refuses it: one Initial packet holding <c>CONNECTION_CLOSE</c>
/// <c>CONNECTION_REFUSED</c> for a version 1 Initial, and no bytes for any other first datagram. A flow a
/// <c>close=graceful</c> drop fault cuts (<see cref="IGracefullyCutDatagramFlow"/>) ends with the QUIC
/// <c>CONNECTION_CLOSE</c> <c>NO_ERROR</c> of type <c>0x1c</c>, which <c>Surl.Quic</c> sends, with no
/// <c>WT_CLOSE_SESSION</c> or <c>GOAWAY</c> (ADR-0188, amendment 6).
/// </remarks>
public sealed class WebTransportProtocolServer : IGracefulCutDatagramProtocolServer, IDatagramRefusalWriter, IExchangeBytesRedactingServer
{
    /// <summary>The scheme the server answers.</summary>
    public const string Scheme = "webtransport";

    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly Func<WebTransportServerCertificate?> certificateSource;
    private readonly TimeSpan idleTimeout;

    /// <summary>Creates the server.</summary>
    /// <param name="authenticationPolicy">The policy every <c>CONNECT</c>'s login is judged by.</param>
    /// <param name="certificateSource">
    /// Gives the server certificate when a flow arrives, because the composition makes the certificate
    /// after it builds its servers; <see langword="null"/> means none is served and no flow is answered.
    /// </param>
    /// <param name="idleTimeout">QUIC's <c>max_idle_timeout</c>; zero for none.</param>
    public WebTransportProtocolServer(
        IAuthenticationPolicy authenticationPolicy, Func<WebTransportServerCertificate?> certificateSource, TimeSpan idleTimeout)
    {
        ArgumentNullException.ThrowIfNull(authenticationPolicy);
        ArgumentNullException.ThrowIfNull(certificateSource);

        this.authenticationPolicy = authenticationPolicy;
        this.certificateSource = certificateSource;
        this.idleTimeout = idleTimeout;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly([Scheme]);

    /// <summary>
    /// Creates the redactor of the decrypted-bytes notes of one HTTP/3 connection: it withholds, from
    /// request header blocks and their decoded header text, the value of every field in
    /// <see cref="HttpWithheldHeaderNames.All"/> (credentials, API keys and session tokens; ADR-0182
    /// decision 7 and amendment 14). A WebTransport session stream - a bidirectional stream starting with
    /// <see cref="WebTransportStreamKind.Bidirectional"/>, or a unidirectional one starting with
    /// <see cref="WebTransportStreamKind.Unidirectional"/> - is logged as received while the redactor
    /// remembers it (amendment 35).
    /// </summary>
    /// <param name="context">The exchange the redactor is for.</param>
    /// <returns>A redactor for that exchange alone.</returns>
    public IReceivedBytesRedactor CreateRedactor(ExchangeRedactionContext context) =>
        new Http3RequestBytesRedactor(
            HttpWithheldHeaderNames.ApiKeysAndSessionTokens,
            (ulong)WebTransportStreamKind.Bidirectional,
            (ulong)WebTransportStreamKind.Unidirectional);

    /// <summary>
    /// Runs one QUIC connection on <paramref name="flow"/> and serves its WebTransport sessions until
    /// it ends or the exchange is cancelled.
    /// </summary>
    /// <param name="flow">The client's datagrams, one remote endpoint.</param>
    /// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
    /// <returns>A task that completes once the connection has ended.</returns>
    public async Task ServeAsync(IDatagramFlow flow, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(context);

        if (certificateSource() is not { } certificate)
        {
            return;
        }

        var headTimeout = WebTransportHeadTimeoutDelay.For(context.Limits.HeadTimeout);
        var options = new QuicServerConnectionOptions
        {
            CertificateChain = certificate.Chain,
            CertificateKey = certificate.Key,
            IdleTimeout = idleTimeout,
            HandshakeTimeout = headTimeout == Timeout.InfiniteTimeSpan ? null : headTimeout,
            DecryptedBytes = context.DecryptedBytes,
            MaxDatagramFrameSize = WebTransportApplicationProtocol.MaxDatagramFrameSize,
            ResetStreamAt = true,
            // HTTP/3's control and two QPACK streams, then the session streams the server announces it takes.
            InitialMaxStreamsUni = 3 + WebTransportFlowControlSettings.Default.InitialMaxStreams,
        };
        using var random = RandomNumberGenerator.Create();
        var application = new StartedApplication(new WebTransportApplicationProtocol(string.Empty, authenticationPolicy, context));

        // Once the connection is served, cancelling the exchange is the HTTP/3 server's to answer, with
        // GOAWAY and CONNECTION_CLOSE H3_NO_ERROR; before then nothing is served, and the flow loop is
        // stopped at once.
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
    /// Refuses a flow past a connection limit: a first datagram that is a version 1 Initial gets one
    /// Initial packet holding <c>CONNECTION_CLOSE</c> <c>CONNECTION_REFUSED</c>; any other gets no bytes.
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

    /// <summary>Tells whether the handshake completed and the connection was handed over to be served.</summary>
    private sealed class StartedApplication(IQuicApplicationProtocol inner) : IQuicApplicationProtocol
    {
        private volatile bool hasStarted;

        public bool HasStarted => hasStarted;

        public Task ServeAsync(QuicApplicationConnection connection, CancellationToken cancellationToken)
        {
            hasStarted = true;
            return inner.ServeAsync(connection, cancellationToken);
        }
    }
}
