using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Networking;

/// <summary>
/// An open client connection to a named host, clear or secured, made by <see cref="TlsClientConnector"/>.
/// Secured means a verified TLS 1.2 or 1.3 client handshake with no client certificate; the clear form
/// can be secured later on the same open stream (StartTLS).
/// </summary>
public sealed class TlsClientConnection : IAsyncDisposable
{
    private readonly string host;
    private readonly int port;
    private readonly DataTransport transport;
    private readonly IReadOnlyList<X509Certificate2> additionalTrustAnchors;
    private readonly TimeProvider timeProvider;

    internal TlsClientConnection(
        string host,
        int port,
        DataTransport transport,
        IReadOnlyList<X509Certificate2> additionalTrustAnchors,
        TimeProvider timeProvider)
    {
        this.host = host;
        this.port = port;
        this.transport = transport;
        this.additionalTrustAnchors = additionalTrustAnchors;
        this.timeProvider = timeProvider;
        Stream = transport.Stream;
    }

    /// <summary>
    /// The stream to read and write: the clear socket stream until TLS runs, the TLS stream after.
    /// </summary>
    public Stream Stream { get; private set; }

    /// <summary>
    /// Whether the TLS handshake has run.
    /// </summary>
    public bool IsSecured => Stream is SslStream;

    /// <summary>
    /// Runs the TLS client handshake on the open connection (RFC 4513 section 3.1.1, after StartTLS's success).
    /// </summary>
    /// <param name="cancellationToken">Cuts the handshake off.</param>
    /// <exception cref="InvalidOperationException">TLS already runs on this connection.</exception>
    /// <exception cref="TlsClientException">The certificate was refused or the handshake failed; the connection is closed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the handshake off.</exception>
    public async ValueTask StartTlsAsync(CancellationToken cancellationToken)
    {
        if (IsSecured)
        {
            throw new InvalidOperationException("TLS already runs on this connection.");
        }

        var check = new TlsServerIdentityCheck(additionalTrustAnchors, timeProvider);
        var secured = new SslStream(transport.Stream, leaveInnerStreamOpen: false);

        try
        {
            await secured.AuthenticateAsClientAsync(OptionsFor(check), cancellationToken);
            Stream = secured;
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException)
        {
            var failure = check.Refusal ?? TlsClientFailure.HandshakeFailed;

            throw new TlsClientException(failure, $"The TLS handshake with {host}:{port} failed: {failure}.", exception);
        }
        finally
        {
            // A failed or cancelled handshake leaves nothing usable: drop the transport at once.
            if (!IsSecured)
            {
                transport.ResetAndRelease();
                secured.Dispose();
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => Stream.DisposeAsync();

    private SslClientAuthenticationOptions OptionsFor(TlsServerIdentityCheck check) =>
        new()
        {
            TargetHost = host,
            EnabledSslProtocols = TlsVersionRange.Default.AcceptedProtocols,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            AllowRenegotiation = false,
            RemoteCertificateValidationCallback = check.Validate,
        };
}
