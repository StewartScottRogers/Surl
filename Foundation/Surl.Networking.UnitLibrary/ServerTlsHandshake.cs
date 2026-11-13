using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Surl.Protocol.Abstractions;
using Surl.TlsSrp;

namespace Surl.Networking;

/// <summary>
/// Runs the server side of a TLS handshake over a connection's stream for one listener
/// (ADR-0010, sections 1, 4 and 5): the process's <see cref="ServerTlsSettings"/> and the ALPN
/// protocol IDs its listen URL's scheme offers.
/// </summary>
internal sealed class ServerTlsHandshake
{
    private readonly ServerTlsSettings settings;

    /// <summary>
    /// Creates the handshake for a listener.
    /// </summary>
    /// <param name="settings">The process's TLS settings.</param>
    /// <param name="applicationProtocols">The ALPN protocol IDs offered; empty offers none.</param>
    public ServerTlsHandshake(ServerTlsSettings settings, IReadOnlyList<SslApplicationProtocol> applicationProtocols)
    {
        this.settings = settings;
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
    /// Runs the handshake over <paramref name="transport"/>: TLS-SRP when the settings carry
    /// <see cref="ServerTlsSettings.TlsSrpAccounts"/> (ADR-0122 decision 5), otherwise
    /// <see cref="SslStream"/>. A failed handshake leaves the transport open.
    /// </summary>
    /// <param name="transport">The connection's plaintext stream.</param>
    /// <param name="cancellationToken">Cuts the handshake off.</param>
    /// <returns>
    /// The secured stream - an <see cref="SslStream"/> or a <see cref="TlsSrpStream"/> - which the
    /// caller disposes before the transport, and the negotiated session.
    /// </returns>
    /// <exception cref="TlsHandshakeException">The handshake failed: an alert, a rejected client certificate, or the peer closing.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the handshake off.</exception>
    public Task<(Stream Stream, TlsSession Session)> AuthenticateAsync(Stream transport, CancellationToken cancellationToken) =>
        settings.TlsSrpAccounts is { } accounts
            ? AuthenticateTlsSrpAsync(new TlsSrpServer(accounts, settings.TlsSrpRandom!, settings.TlsSrpCertificate), transport, cancellationToken)
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

    private static TlsSession SessionOf(SslStream sslStream) =>
        new(
            sslStream.SslProtocol,
            sslStream.NegotiatedCipherSuite,
            ApplicationProtocolOf(sslStream.NegotiatedApplicationProtocol),
            string.IsNullOrEmpty(sslStream.TargetHostName) ? null : sslStream.TargetHostName,
            CopyOf(sslStream.RemoteCertificate));

    // RFC 5054's suite numbers are IANA's, as TlsCipherSuite's are.
    private static TlsSession SessionOf(TlsSrpSession session) =>
        new(SslProtocols.Tls12, (TlsCipherSuite)(ushort)session.CipherSuite, session.ApplicationProtocol, session.ServerName, null)
        {
            SrpUserName = session.UserName,
        };

    private static string? ApplicationProtocolOf(SslApplicationProtocol negotiated) =>
        negotiated.Protocol.IsEmpty ? null : negotiated.ToString();

    // SslStream disposes its remote certificate with itself; the session keeps its own copy.
    private static X509Certificate2? CopyOf(X509Certificate? certificate) =>
        certificate is null ? null : X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
}
