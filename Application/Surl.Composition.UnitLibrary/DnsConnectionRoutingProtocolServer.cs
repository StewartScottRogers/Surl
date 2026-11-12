using Surl.Extended.Protocol.DnsQuery;
using Surl.HttpServing;
using Surl.Http2;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Serves the connection side of <c>dns</c>: a listen URL marked <see cref="ListenUrl.IsDnsOverHttps"/>
/// (which <see cref="DnsTransportComposition"/> sets for <c>--dns-transport https</c>) is DNS over HTTPS
/// (RFC 8484, ADR-0092 decision 5), every other one is DNS over TCP or TLS. The serving engine picks one
/// connection server per scheme, and both answer <c>dns</c>, so this one picks per exchange.
/// </summary>
/// <remarks>
/// A connection whose handshake agreed ALPN <c>h2</c> is served as HTTP/2 from its first bytes; any
/// other (<c>http/1.1</c> or none) as HTTP/1.1, both by one <see cref="DnsOverHttpsHandler"/> over the
/// zones. There is no DoH over HTTP/3 (ADR-0086 decision 6).
/// </remarks>
internal sealed class DnsConnectionRoutingProtocolServer(
    IConnectionProtocolServer streamServer,
    DnsZoneSet zones,
    bool allowZoneTransfer) : IConnectionProtocolServer
{
    private const string Http2ApplicationProtocol = "h2";

    /// <summary>The server for DNS over TCP and TLS, which answers every listen URL that is not DNS over HTTPS.</summary>
    internal IConnectionProtocolServer StreamServer => streamServer;

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes => streamServer.Schemes;

    /// <inheritdoc/>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);
        if (!context.ListenUrl.IsDnsOverHttps)
        {
            return streamServer.ServeAsync(connection, context);
        }

        var handler = new DnsOverHttpsHandler(zones, context, allowZoneTransfer);
        return connection.TlsSession is { ApplicationProtocol: Http2ApplicationProtocol }
            ? ServeHttp2Async(handler, connection, context)
            : new Http1ConnectionServer(handler, context).ServeAsync(connection);
    }

    // The client preface is read first, as HttpProtocolServer does; bytes that are not the preface close the
    // connection with none written, and the engine's idle timeout and maximum duration cut a silent client off.
    private static async Task ServeHttp2Async(DnsOverHttpsHandler handler, IConnection connection, ExchangeContext context)
    {
        var preface = new Http2ConnectionPreface();
        await preface.ReadAsync(connection, context.CancellationToken);
        if (preface.IsComplete)
        {
            await new Http2ConnectionServer(handler, context, new AlreadyReadBytesConnection(connection, preface.BytesReadAfterPreface)).ServeAsync();
        }
    }
}
