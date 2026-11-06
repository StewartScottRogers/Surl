using System.Net.Security;
using System.Security.Authentication;
using Surl.Content;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Protocol.Http;

/// <summary>
/// The <see cref="IQuicApplicationProtocol"/> <see cref="Http3ProtocolServer"/> hands each QUIC
/// connection to once its handshake is complete: it starts the connection's authentication
/// session as an encrypted one, with the TLS 1.3 session QUIC agreed and no client certificate,
/// and serves the connection with an <see cref="Http3ConnectionServer"/>.
/// </summary>
/// <param name="contentStore">What GET and HEAD serve.</param>
/// <param name="authenticationPolicy">The policy every request is judged by.</param>
/// <param name="context">The exchange.</param>
internal sealed class Http3ApplicationProtocol(
    ContentStore contentStore, IAuthenticationPolicy authenticationPolicy, ExchangeContext context) : IQuicApplicationProtocol
{
    private volatile bool hasStarted;

    /// <summary>Whether the handshake completed and the connection was handed over to be served.</summary>
    public bool HasStarted => hasStarted;

    /// <inheritdoc/>
    public Task ServeAsync(QuicApplicationConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        hasStarted = true;

        // Surl.Tls13 numbers its cipher suites by their IANA values, as TlsCipherSuite does.
        var tlsSession = new TlsSession(
            SslProtocols.Tls13, (TlsCipherSuite)(ushort)connection.CipherSuite, connection.ApplicationProtocol, ServerName: null, ClientCertificate: null);
        var answerer = new HttpRequestAnswerer(context, contentStore, authenticationPolicy.StartHttpConnection(tlsSession));
        var options = new Http3ServerOptions { ServerFieldValue = HttpResponseHead.ServerName };
        return new Http3ConnectionServer(new Http3StreamAnswerWriter(answerer, context), options, context).ServeAsync(connection, cancellationToken);
    }
}
