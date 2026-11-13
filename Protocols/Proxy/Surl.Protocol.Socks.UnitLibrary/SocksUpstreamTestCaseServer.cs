using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// The SOCKS server half of an upstream test case (ADR-0080 decision 2): <see cref="SocksProtocolServer"/>
/// with the case's <see cref="SocksUpstreamTestCaseReplies"/> applied - its login requirement, its
/// loopback-only origin and its forced failure. Which SOCKS version it answers is the listener's
/// scheme, as for the shipped server. Never compose it into the shipped <c>surl</c>.
/// </summary>
public sealed class SocksUpstreamTestCaseServer : IConnectionProtocolServer
{
    private readonly SocksProtocolServer server;

    /// <summary>
    /// Creates the server half for one case.
    /// </summary>
    /// <param name="replies">The case's server commands.</param>
    /// <param name="loopbackConnector">How the loopback origin is reached.</param>
    public SocksUpstreamTestCaseServer(SocksUpstreamTestCaseReplies replies, IOriginConnector loopbackConnector)
    {
        ArgumentNullException.ThrowIfNull(replies);
        ArgumentNullException.ThrowIfNull(loopbackConnector);

        server = new SocksProtocolServer(new SocksUpstreamTestCaseOriginConnector(replies, loopbackConnector), replies.Accounts);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes => server.Schemes;

    /// <inheritdoc/>
    public Task ServeAsync(IConnection connection, ExchangeContext context) => server.ServeAsync(connection, context);
}
