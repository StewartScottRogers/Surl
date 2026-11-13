using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Networking;

/// <summary>
/// Opens TCP connections to a host the operator names - an LDAP account source, ADR-0149 decision 5 -
/// and runs a verified TLS 1.2 or 1.3 client handshake at once (<c>ldaps</c>) or later on the open
/// stream (StartTLS, <see cref="TlsClientConnection.StartTlsAsync"/>). The chain must build to a
/// system root or one of the given certificates and the certificate must name the host; no option
/// skips either. No allowed-origin rule applies, because the operator, not a peer, chose the host.
/// </summary>
public sealed class TlsClientConnector
{
    private readonly IReadOnlyList<X509Certificate2> additionalTrustAnchors;
    private readonly TimeProvider timeProvider;
    private readonly IOriginSockets sockets;

    /// <summary>
    /// Creates the connector.
    /// </summary>
    /// <param name="additionalTrustAnchors">Certificates trusted beside the system roots (<c>ca-file</c>); may be empty.</param>
    /// <param name="timeProvider">Times the timeout and chain validity; <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public TlsClientConnector(IReadOnlyList<X509Certificate2> additionalTrustAnchors, TimeProvider? timeProvider = null)
        : this(additionalTrustAnchors, timeProvider ?? TimeProvider.System, new SocketOriginSockets())
    {
    }

    internal TlsClientConnector(
        IReadOnlyList<X509Certificate2> additionalTrustAnchors, TimeProvider timeProvider, IOriginSockets sockets)
    {
        ArgumentNullException.ThrowIfNull(additionalTrustAnchors);

        this.additionalTrustAnchors = additionalTrustAnchors;
        this.timeProvider = timeProvider;
        this.sockets = sockets;
    }

    /// <summary>
    /// Resolves <paramref name="host"/>, connects to its addresses in the resolver's order until one
    /// accepts, and runs the handshake when <paramref name="secureAtOnce"/>.
    /// </summary>
    /// <param name="host">A DNS name or an IP literal without brackets; also the name the certificate must carry.</param>
    /// <param name="port">1 to 65535.</param>
    /// <param name="secureAtOnce"><see langword="true"/> for <c>ldaps</c>; <see langword="false"/> to leave the stream clear.</param>
    /// <param name="timeout">Covers resolution, every connect attempt and, when secured at once, the handshake.</param>
    /// <param name="cancellationToken">Cuts the connect off.</param>
    /// <returns>The open connection.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/> is not 1 to 65535.</exception>
    /// <exception cref="TlsClientException">The connection or handshake failed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the connect off.</exception>
    public async ValueTask<TlsClientConnection> ConnectAsync(
        string host, int port, bool secureAtOnce, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, IPEndPoint.MinPort + 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, IPEndPoint.MaxPort);

        using var timeoutSource = new CancellationTokenSource(timeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            var transport = await ConnectFirstAsync(host, port, await ResolveAsync(host, linked.Token), linked.Token);
            var connection = new TlsClientConnection(host, port, transport, additionalTrustAnchors, timeProvider);

            if (secureAtOnce)
            {
                await connection.StartTlsAsync(linked.Token);
            }

            return connection;
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TlsClientException(TlsClientFailure.TimedOut, $"{host}:{port} did not finish connecting within {timeout}.");
        }
    }

    private async Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            var addresses = await sockets.ResolveAsync(host, cancellationToken);

            return addresses.Length > 0 ? addresses : throw HostNotFound(host, "it has no addresses", null);
        }
        catch (SocketException exception)
        {
            throw HostNotFound(host, exception.Message, exception);
        }
    }

    private async Task<DataTransport> ConnectFirstAsync(
        string host, int port, IPAddress[] addresses, CancellationToken cancellationToken)
    {
        TlsClientException? lastFailure = null;

        foreach (var target in addresses.Select(address => new IPEndPoint(DataConnectionPeer.Normalize(address), port)).Distinct())
        {
            try
            {
                return await sockets.ConnectAsync(target, cancellationToken);
            }
            catch (SocketException exception)
            {
                lastFailure = new TlsClientException(
                    exception.SocketErrorCode == SocketError.ConnectionRefused ? TlsClientFailure.Refused : TlsClientFailure.Unreachable,
                    $"{host} could not be reached at {target}: {exception.Message}",
                    exception);
            }
        }

        throw lastFailure!;
    }

    private static TlsClientException HostNotFound(string host, string reason, Exception? inner) =>
        new(TlsClientFailure.HostNotFound, $"{host} did not resolve: {reason}.", inner);
}
