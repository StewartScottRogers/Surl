using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Surl.Protocol.Abstractions;
using Surl.Tls13Stream;
using Surl.TlsSrp;
using Tls13AlertException = Surl.Tls13.TlsAlertException;

namespace Surl.Networking;

/// <summary>
/// Runs the server side of a TLS handshake over a connection's stream for one listener
/// (ADR-0010, sections 1, 4 and 5): the process's <see cref="ServerTlsSettings"/> and the ALPN
/// protocol IDs its listen URL's scheme offers.
/// </summary>
internal sealed class ServerTlsHandshake
{
    private readonly ServerTlsSettings settings;
    private readonly ITls13Support tls13Support;

    /// <summary>
    /// Creates the handshake for a listener.
    /// </summary>
    /// <param name="settings">The process's TLS settings.</param>
    /// <param name="applicationProtocols">The ALPN protocol IDs offered; empty offers none.</param>
    /// <param name="tls13Support">Says whether <see cref="SslStream"/> can serve TLS 1.3; the platform's answer when <see langword="null"/>.</param>
    public ServerTlsHandshake(ServerTlsSettings settings, IReadOnlyList<SslApplicationProtocol> applicationProtocols, ITls13Support? tls13Support = null)
    {
        this.settings = settings;
        this.tls13Support = tls13Support ?? new PlatformTls13Support();
        ApplicationProtocols = applicationProtocols;
    }

    /// <summary>
    /// The ALPN protocol IDs this listener offers.
    /// </summary>
    public IReadOnlyList<SslApplicationProtocol> ApplicationProtocols { get; }

    /// <summary>
    /// The handshake for a listener on <paramref name="listenUrl"/>, or <see langword="null"/>
    /// when the process has no TLS settings.
    /// </summary>
    /// <param name="settings">The process's TLS settings, or <see langword="null"/>.</param>
    /// <param name="listenUrl">The listener's listen URL; its scheme, and whether it is a proxy's, pick the ALPN protocol IDs.</param>
    /// <returns>The handshake, or <see langword="null"/>.</returns>
    public static ServerTlsHandshake? ForListener(ServerTlsSettings? settings, ListenUrl listenUrl) =>
        settings is null ? null : new ServerTlsHandshake(settings, TlsApplicationProtocols.ForListener(listenUrl));

    /// <summary>
    /// Runs the handshake over <paramref name="transport"/> by one of three paths, chosen in this
    /// order. A failed handshake leaves the transport open.
    /// <list type="number">
    /// <item><description>TLS-SRP, through <see cref="TlsSrpServer"/>, when the settings carry
    /// <see cref="ServerTlsSettings.TlsSrpAccounts"/> (ADR-0122 decision 5).</description></item>
    /// <item><description>The hand-built TLS 1.3 stream, <see cref="Tls13ServerStream"/>, only when
    /// the settings serve a certificate and accept TLS 1.3 and <see cref="ITls13Support"/> says
    /// <see cref="SslStream"/> has no TLS 1.3 server (ADR-0157 decision 1 and amendment 1). The first
    /// record is read, and this stream serves a ClientHello that offers TLS 1.3 and nothing lower.
    /// When the lowest accepted version is TLS 1.3, it serves every ClientHello that offers TLS 1.3,
    /// and a parsed one that does not is refused with a fatal <c>protocol_version</c>
    /// alert.</description></item>
    /// <item><description><see cref="SslStream"/> for every other handshake, including, after a first
    /// record was read, any other ClientHello not refused above and any bytes that do not parse as one
    /// whole ClientHello record; the bytes read are replayed to it.</description></item>
    /// </list>
    /// </summary>
    /// <param name="transport">The connection's plaintext stream.</param>
    /// <param name="cancellationToken">Cuts the handshake off.</param>
    /// <param name="loginJudge">
    /// Asked and told about a TLS-SRP login once the client's <c>Finished</c> verifies (ADR-0165);
    /// an <see cref="SslStream"/> or TLS 1.3 handshake never calls it. <see langword="null"/> for none.
    /// </param>
    /// <returns>
    /// The secured stream - a <see cref="TlsSrpStream"/>, a <see cref="Tls13ServerStream"/> or an
    /// <see cref="SslStream"/> - which the caller disposes before the transport, and the negotiated
    /// session.
    /// </returns>
    /// <exception cref="TlsHandshakeException">
    /// The handshake failed: an alert, a rejected client certificate, the peer closing, or a
    /// ClientHello refused for not offering TLS 1.3.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the handshake off.</exception>
    public Task<(Stream Stream, TlsSession Session)> AuthenticateAsync(
        Stream transport, CancellationToken cancellationToken, ITlsLoginJudge? loginJudge = null) =>
        settings.TlsSrpAccounts is { } accounts
            ? AuthenticateTlsSrpAsync(
                new TlsSrpServer(
                    accounts,
                    settings.TlsSrpRandom!,
                    settings.TlsSrpCertificate,
                    loginJudge is null ? null : new TlsSrpLoginJudgeAdapter(loginJudge)),
                transport,
                cancellationToken)
            : settings.CanServeTls13Stream && tls13Support.SslStreamLacksTls13Server
                ? AuthenticateByClientHelloAsync(transport, cancellationToken)
                : AuthenticateSslStreamAsync(transport, cancellationToken);

    private static async Task<(Stream Stream, TlsSession Session)> AuthenticateTlsSrpAsync(
        TlsSrpServer server, Stream transport, CancellationToken cancellationToken)
    {
        try
        {
            var session = await server.AuthenticateAsync(transport, cancellationToken);

            return (session.Stream, SessionOf(session));
        }
        catch (Exception exception) when (exception is TlsAlertException or IOException)
        {
            throw new TlsHandshakeException(exception.Message, exception);
        }
    }

    // ADR-0157 decision 1 and amendment 1: the platform's SslStream has no TLS 1.3 server, so the
    // first record is read and the handshake goes one of three ways:
    // - the hand-built stream serves a ClientHello that offers TLS 1.3 and nothing lower and, on a
    //   listener limited to TLS 1.3, every ClientHello that offers TLS 1.3;
    // - on a listener limited to TLS 1.3, a parsed ClientHello that does not offer TLS 1.3 is
    //   refused with a fatal protocol_version alert;
    // - SslStream gets every other ClientHello, and any bytes that do not parse as one whole
    //   ClientHello record, with the bytes read replayed.
    private async Task<(Stream Stream, TlsSession Session)> AuthenticateByClientHelloAsync(
        Stream transport, CancellationToken cancellationToken)
    {
        ClientHelloPeek peek;

        try
        {
            peek = await ClientHelloPeek.ReadAsync(transport, cancellationToken);
        }
        catch (IOException exception)
        {
            throw new TlsHandshakeException(exception.Message, exception);
        }

        // ADR-0157 amendment 1: a listener limited to TLS 1.3 serves every hello that lists TLS 1.3.
        var tls13Only = settings.AcceptedVersions.Lowest == SslProtocols.Tls13;
        if (tls13Only && peek.ClientHello is not null && !peek.OffersTls13)
        {
            throw await WriteProtocolVersionAlertAsync(transport, cancellationToken);
        }

        return peek.OffersOnlyTls13 || (tls13Only && peek.OffersTls13)
            ? await AuthenticateTls13Async(transport, peek, cancellationToken)
            : await AuthenticateSslStreamAsync(new ReplayedPrefixStream(peek.BytesRead, transport), cancellationToken);
    }

    // RFC 8446 section 4.2.1: a TLS 1.3-only server answers a hello without TLS 1.3 with a fatal protocol_version alert.
    // Writes the alert and returns the refusal for the caller to throw, so the throw is visible at the call site.
    private static async Task<TlsHandshakeException> WriteProtocolVersionAlertAsync(Stream transport, CancellationToken cancellationToken)
    {
        byte[] alert = [21, 3, 3, 0, 2, 2, 70];

        try
        {
            await transport.WriteAsync(alert, cancellationToken);
            await transport.FlushAsync(cancellationToken);
        }
        catch (IOException)
        {
            // The peer is gone; the refusal below is the same.
        }

        return new TlsHandshakeException("The client offers no TLS version this listener accepts (TLS 1.3 only).", null);
    }

    private async Task<(Stream Stream, TlsSession Session)> AuthenticateTls13Async(
        Stream transport, ClientHelloPeek peek, CancellationToken cancellationToken)
    {
        var options = settings.CreateTls13Options(ApplicationProtocols, out var certificateKey);
        using var random = RandomNumberGenerator.Create();

        try
        {
            var stream = await Tls13ServerStream.AcceptAsync(transport, options, random, peek.BytesRead, cancellationToken);

            return (stream, SessionOf(stream));
        }
        catch (Exception exception) when (exception is Tls13AlertException or IOException)
        {
            throw new TlsHandshakeException(exception.Message, exception);
        }
        finally
        {
            certificateKey.Dispose();
        }
    }

    private async Task<(Stream Stream, TlsSession Session)> AuthenticateSslStreamAsync(Stream transport, CancellationToken cancellationToken)
    {
        var sslStream = new SslStream(transport, leaveInnerStreamOpen: true);
        var succeeded = false;

        try
        {
            await sslStream.AuthenticateAsServerAsync(settings.CreateAuthenticationOptions(ApplicationProtocols), cancellationToken);
            succeeded = true;

            return (sslStream, SessionOf(sslStream));
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException)
        {
            throw new TlsHandshakeException(exception.Message, exception);
        }
        finally
        {
            // Releases the TLS state only; the transport was left open on purpose.
            if (!succeeded)
            {
                sslStream.Dispose();
            }
        }
    }

    // The Tls13 library's suite numbers are IANA's, as TlsCipherSuite's are.
    private static TlsSession SessionOf(Tls13ServerStream stream) =>
        new(SslProtocols.Tls13, (System.Net.Security.TlsCipherSuite)(ushort)stream.CipherSuite, stream.ApplicationProtocol, stream.ClientHello.ServerName, ClientCertificateOf(stream));

    // The stream verified the chain before the handshake finished; the session keeps the client's own certificate.
    private static X509Certificate2? ClientCertificateOf(Tls13ServerStream stream) =>
        stream.ClientCertificateChain is { } chain ? X509CertificateLoader.LoadCertificate(chain[0].Span) : null;

    private static TlsSession SessionOf(SslStream sslStream) =>
        new(
            sslStream.SslProtocol,
            sslStream.NegotiatedCipherSuite,
            ApplicationProtocolOf(sslStream.NegotiatedApplicationProtocol),
            string.IsNullOrEmpty(sslStream.TargetHostName) ? null : sslStream.TargetHostName,
            CopyOf(sslStream.RemoteCertificate));

    // RFC 5054's suite numbers are IANA's, as TlsCipherSuite's are.
    private static TlsSession SessionOf(TlsSrpSession session) =>
        new(SslProtocols.Tls12, (System.Net.Security.TlsCipherSuite)(ushort)session.CipherSuite, session.ApplicationProtocol, session.ServerName, null)
        {
            SrpUserName = session.UserName,
        };

    private static string? ApplicationProtocolOf(SslApplicationProtocol negotiated) =>
        negotiated.Protocol.IsEmpty ? null : negotiated.ToString();

    // SslStream disposes its remote certificate with itself; the session keeps its own copy.
    private static X509Certificate2? CopyOf(X509Certificate? certificate) =>
        certificate is null ? null : X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
}
