using System.Diagnostics.CodeAnalysis;
using System.Security.Authentication;
using Surl.Authentication;
using Surl.Networking;

namespace Surl.Composition;

/// <summary>
/// Adapts <see cref="TlsClientConnector"/> to the seam <see cref="LdapAccountSource"/> reaches a directory
/// through (ADR-0149 decision 11): a directory that cannot be reached surfaces as an
/// <see cref="IOException"/>, one whose certificate or handshake is refused as an
/// <see cref="AuthenticationException"/>, so the source notes the right reason.
/// </summary>
/// <param name="connector">Opens the connections and runs the verified handshakes.</param>
/// <param name="timeout">Covers resolution, connecting and, for <c>ldaps</c>, the handshake.</param>
[ExcludeFromCodeCoverage(Justification = "Opens sockets; covered by the integration tests in TlsClientLdapDirectoryConnectorTests.")]
internal sealed class TlsClientLdapDirectoryConnector(TlsClientConnector connector, TimeSpan timeout) : ILdapDirectoryConnector
{
    /// <inheritdoc/>
    public async ValueTask<ILdapDirectoryConnection> ConnectAsync(LdapDirectoryEndpoint endpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        try
        {
            var connection = await connector.ConnectAsync(endpoint.Host, endpoint.Port, endpoint.UseTls, timeout, cancellationToken)
                .ConfigureAwait(false);

            return new Connection(connection);
        }
        catch (TlsClientException exception) when (IsTlsRefusal(exception))
        {
            throw new AuthenticationException(exception.Message, exception);
        }
    }

    private static bool IsTlsRefusal(TlsClientException exception) =>
        exception.Failure is TlsClientFailure.UntrustedCertificate or TlsClientFailure.CertificateNameMismatch or TlsClientFailure.HandshakeFailed;

    private sealed class Connection(TlsClientConnection connection) : ILdapDirectoryConnection
    {
        public Stream Stream => connection.Stream;

        public async ValueTask StartTlsAsync(CancellationToken cancellationToken)
        {
            try
            {
                await connection.StartTlsAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (TlsClientException exception)
            {
                throw new AuthenticationException(exception.Message, exception);
            }
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
