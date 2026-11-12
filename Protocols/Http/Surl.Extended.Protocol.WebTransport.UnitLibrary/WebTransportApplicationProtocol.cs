using System.Net.Security;
using System.Security.Authentication;
using Surl.Http3;
using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// The WebTransport server on one QUIC connection (ADR-0117): the <see cref="IQuicApplicationProtocol"/>
/// a <see cref="QuicDatagramFlowServer"/> hands each connection to once its handshake has agreed
/// <c>h3</c>. It serves HTTP/3 with an <see cref="Http3ConnectionServer"/> that registers
/// <c>webtransport-h3</c> (with stream signal <c>0x41</c> and stream type <c>0x54</c>) and sends
/// <c>SETTINGS_WT_ENABLED</c> = 1 and the three <c>SETTINGS_WT_INITIAL_*</c> of decision 4; every
/// request that is not a WebTransport <c>CONNECT</c> is answered <c>405</c>. Each <c>CONNECT</c>'s
/// login is judged by the connection's session of the authentication policy (decision 7).
/// </summary>
/// <remarks>
/// The server composing it sets <c>QuicServerConnectionOptions.MaxDatagramFrameSize</c> to
/// <see cref="MaxDatagramFrameSize"/> and <c>ResetStreamAt</c>, which WTH3 3.1 requires.
/// </remarks>
public sealed class WebTransportApplicationProtocol : IQuicApplicationProtocol
{
    /// <summary>The <c>max_datagram_frame_size</c> transport parameter the server sends (ADR-0117 decision 0; RFC 9221 section 3).</summary>
    public const ulong MaxDatagramFrameSize = 65535;

    /// <summary>How long the connection keeps serving sessions after shutdown begins, before it closes them (ADR-0117 decision 6): the same five seconds as the serving engine's default.</summary>
    public static readonly TimeSpan ShutdownGrace = TimeSpan.FromSeconds(5);

    private readonly string acceptedPath;
    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly WebTransportFlowControlSettings flowControlSettings;
    private readonly ExchangeContext context;

    /// <summary>Creates the server with ADR-0117 decision 4's flow-control limits.</summary>
    /// <param name="acceptedPath">The listen URL's path: it and every path below it at a <c>/</c> accept a session; empty or <c>/</c> accepts every path.</param>
    /// <param name="authenticationPolicy">The policy every <c>CONNECT</c>'s login is judged by: <c>--user</c>, <c>--user-file</c> and <c>--allow-anonymous</c>.</param>
    /// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
    public WebTransportApplicationProtocol(string acceptedPath, IAuthenticationPolicy authenticationPolicy, ExchangeContext context)
        : this(acceptedPath, authenticationPolicy, WebTransportFlowControlSettings.Default, context)
    {
    }

    /// <summary>Creates the server with the flow-control limits <paramref name="flowControlSettings"/>.</summary>
    /// <param name="acceptedPath">The listen URL's path.</param>
    /// <param name="authenticationPolicy">The policy every <c>CONNECT</c>'s login is judged by.</param>
    /// <param name="flowControlSettings">The flow-control limits offered each session.</param>
    /// <param name="context">The exchange.</param>
    internal WebTransportApplicationProtocol(string acceptedPath, IAuthenticationPolicy authenticationPolicy, WebTransportFlowControlSettings flowControlSettings, ExchangeContext context)
    {
        this.acceptedPath = acceptedPath ?? throw new ArgumentNullException(nameof(acceptedPath));
        this.authenticationPolicy = authenticationPolicy ?? throw new ArgumentNullException(nameof(authenticationPolicy));
        this.flowControlSettings = flowControlSettings;
        this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public Task ServeAsync(QuicApplicationConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        // QUIC is always TLS 1.3 (ADR-0082); Surl.Tls13 numbers its cipher suites by their IANA values, as TlsCipherSuite does.
        var tlsSession = new TlsSession(
            SslProtocols.Tls13, (TlsCipherSuite)(ushort)connection.CipherSuite, connection.ApplicationProtocol, ServerName: null, ClientCertificate: null);
        var authentication = new WebTransportConnectAuthentication(authenticationPolicy.StartHttpConnection(tlsSession));
        var directory = new WebTransportSessionDirectory(context);
        var options = new Http3ServerOptions
        {
            ExtendedConnectHandlers = [new WebTransportConnectHandler(acceptedPath, authentication, flowControlSettings, directory, context)],
            FurtherSettings = [new(WebTransportHttp3Codes.EnabledSetting, 1), .. flowControlSettings.ToHttp3Settings()],
            ShutdownGrace = ShutdownGrace,
        };
        return new Http3ConnectionServer(new WebTransportRequestStreamHandler(directory), options, context).ServeAsync(connection, cancellationToken);
    }
}
