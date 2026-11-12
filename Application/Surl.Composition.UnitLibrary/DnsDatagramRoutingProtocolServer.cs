using Surl.Extended.Protocol.DnsQuery;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Serves the datagram side of <c>dns</c>: a listen URL that is TLS from the first byte
/// (<see cref="ListenUrl.IsImplicitTls"/>, which <see cref="DnsTransportComposition"/> sets for
/// <c>--dns-transport quic</c>) is DNS over QUIC (ADR-0092 decision 5), every other one is plain UDP.
/// The serving engine picks one datagram server per scheme, and both answer <c>dns</c>, so this one
/// picks per exchange.
/// </summary>
/// <remarks>
/// The DNS over QUIC server is built when its first flow arrives: the registration builds servers
/// before server TLS has made the certificate (as <see cref="DeferredCoapsProtocolServer"/> does).
/// </remarks>
internal sealed class DnsDatagramRoutingProtocolServer(
    IDatagramProtocolServer udpServer,
    Func<DnsOverQuicProtocolServer> createQuicServer) : IDatagramProtocolServer
{
    private readonly Lazy<DnsOverQuicProtocolServer> quicServer = new(createQuicServer);

    /// <summary>The plain UDP server, which answers every listen URL that is not DNS over QUIC.</summary>
    internal IDatagramProtocolServer UdpServer => udpServer;

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes => udpServer.Schemes;

    /// <inheritdoc/>
    public Task ServeAsync(IDatagramFlow flow, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return (context.ListenUrl.IsImplicitTls ? quicServer.Value : udpServer).ServeAsync(flow, context);
    }
}
