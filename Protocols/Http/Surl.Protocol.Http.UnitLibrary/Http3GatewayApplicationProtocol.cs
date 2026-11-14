using Surl.Http3;
using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Protocol.Http;

/// <summary>
/// The <see cref="IQuicApplicationProtocol"/> <see cref="Http3GatewayUpstreamTestCaseServer"/> hands each
/// QUIC connection to once its handshake is complete: an <see cref="Http3ConnectionServer"/> whose
/// request streams are forwarded by <see cref="Http3GatewayStreamForwarder"/>.
/// </summary>
/// <param name="backend">The case's <c>http</c> half.</param>
/// <param name="context">The exchange.</param>
internal sealed class Http3GatewayApplicationProtocol(NghttpxGatewayBackend backend, ExchangeContext context) : IQuicApplicationProtocol
{
    private volatile bool hasStarted;

    /// <summary>Whether the handshake completed and the connection was handed over to be served.</summary>
    public bool HasStarted => hasStarted;

    /// <inheritdoc/>
    public async Task ServeAsync(QuicApplicationConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        hasStarted = true;
        await new Http3ConnectionServer(new Http3GatewayStreamForwarder(backend), new Http3ServerOptions(), context)
            .ServeAsync(connection, cancellationToken);
    }
}
