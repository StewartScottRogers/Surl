using System.Security.Cryptography;
using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The DNS server over QUIC (RFC 9250; ADR-0092 decision 5): each datagram flow is one QUIC
/// connection, run by a <see cref="QuicDatagramFlowServer"/> offering ALPN <c>doq</c> only with
/// the server certificate, whose queries a <see cref="DnsOverQuicConnectionServer"/> answers.
/// </summary>
/// <remarks>
/// The transport parameters are ADR-0092 decision 5's: <c>initial_max_streams_bidi</c> 100,
/// <c>initial_max_streams_uni</c> 0, <c>max_idle_timeout</c> the idle timeout it is given, and no
/// <c>max_datagram_frame_size</c>. The handshake must complete within the exchange's head timeout
/// (ADR-0092 decision 6). No session ticket is issued, so no 0-RTT query is ever accepted (RFC 9250
/// section 4.5).
/// </remarks>
public sealed class DnsOverQuicProtocolServer : IDatagramProtocolServer
{
    /// <summary>The one ALPN protocol offered (RFC 9250 section 4.1.1).</summary>
    public const string ApplicationProtocol = "doq";

    /// <summary>How many queries one connection may have open at once: <c>initial_max_streams_bidi</c>.</summary>
    public const ulong MaxConcurrentQueries = 100;

    private static readonly TimeSpan MaxTimerDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1.0);

    private readonly DnsQueryAnswerer answerer;
    private readonly IReadOnlyList<ReadOnlyMemory<byte>> certificateChain;
    private readonly AsymmetricAlgorithm certificateKey;
    private readonly TimeSpan idleTimeout;

    /// <summary>
    /// Creates a DNS over QUIC server that answers from <paramref name="zones"/>, refusing zone
    /// transfer unless <paramref name="allowZoneTransfer"/>.
    /// </summary>
    /// <param name="zones">The zones answered from.</param>
    /// <param name="certificateChain">The server certificate and its intermediates, DER, leaf first.</param>
    /// <param name="certificateKey">The certificate's private key: RSA, or ECDSA on P-256, P-384 or P-521.</param>
    /// <param name="idleTimeout">QUIC's <c>max_idle_timeout</c>; zero for none.</param>
    /// <param name="allowZoneTransfer">Whether AXFR and IXFR at a served apex are answered with the zone.</param>
    public DnsOverQuicProtocolServer(
        DnsZoneSet zones,
        IReadOnlyList<ReadOnlyMemory<byte>> certificateChain,
        AsymmetricAlgorithm certificateKey,
        TimeSpan idleTimeout,
        bool allowZoneTransfer = false)
    {
        ArgumentNullException.ThrowIfNull(certificateChain);
        ArgumentNullException.ThrowIfNull(certificateKey);
        answerer = new DnsQueryAnswerer(zones, allowZoneTransfer);
        this.certificateChain = certificateChain;
        this.certificateKey = certificateKey;
        this.idleTimeout = idleTimeout;
    }

    /// <summary>The one scheme answered: <c>dns</c>.</summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["dns"]);

    /// <summary>
    /// Runs one QUIC connection on <paramref name="flow"/> and answers its queries until it ends or
    /// the exchange is cancelled.
    /// </summary>
    /// <param name="flow">The client's datagrams, one remote endpoint.</param>
    /// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
    /// <returns>A task that completes once the connection has ended.</returns>
    public async Task ServeAsync(IDatagramFlow flow, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(context);

        using var random = RandomNumberGenerator.Create();
        var application = new DnsOverQuicConnectionServer(answerer, context);
        var started = new StartedFlag(application);

        // Once DNS over QUIC is served, cancelling the exchange is its server's to answer, by
        // returning, which closes the connection with DOQ_NO_ERROR; before then nothing is served,
        // and the flow loop is stopped at once.
        using var stopBeforeServing = new CancellationTokenSource();
        await using var registration = context.CancellationToken.Register(() =>
        {
            if (!started.HasStarted)
            {
                stopBeforeServing.Cancel();
            }
        });
        await new QuicDatagramFlowServer(Options(context), started, context.TimeProvider, random).ServeAsync(flow, stopBeforeServing.Token);
    }

    /// <summary>The QUIC options for one connection, ADR-0092 decision 5's transport parameters.</summary>
    internal QuicServerConnectionOptions Options(ExchangeContext context)
    {
        var headTimeout = context.Limits.HeadTimeout;
        return new QuicServerConnectionOptions
        {
            CertificateChain = certificateChain,
            CertificateKey = certificateKey,
            ApplicationProtocols = [ApplicationProtocol],
            IdleTimeout = idleTimeout,
            HandshakeTimeout = headTimeout == Timeout.InfiniteTimeSpan || headTimeout > MaxTimerDelay ? null : headTimeout,
            InitialMaxStreamsBidi = MaxConcurrentQueries,
            InitialMaxStreamsUni = 0,
        };
    }

    /// <summary>Hands the connection to <paramref name="application"/>, noting that it was.</summary>
    private sealed class StartedFlag(DnsOverQuicConnectionServer application) : IQuicApplicationProtocol
    {
        private volatile bool hasStarted;

        public bool HasStarted => hasStarted;

        public Task ServeAsync(QuicApplicationConnection connection, CancellationToken cancellationToken)
        {
            hasStarted = true;
            return application.ServeAsync(connection, cancellationToken);
        }
    }
}
