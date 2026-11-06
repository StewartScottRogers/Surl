using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// The SOCKS server (ADR-0121 decisions 1, 2 and 6): the proxy curl's <c>--socks4</c>,
/// <c>--socks4a</c> and <c>-x</c>/<c>--proxy socks4://</c> or <c>socks4a://</c> talk to. It
/// connects each request through the injected <see cref="IOriginConnector"/> and relays bytes
/// both ways.
/// </summary>
/// <remarks>
/// <para>
/// The first byte picks the version. <c>04</c> is a SOCKS4 request: <c>CD</c>, <c>DSTPORT</c>,
/// <c>DSTIP</c>, a user ID and its zero, and, when <c>DSTIP</c> is <c>0.0.0.x</c> with <c>x</c>
/// not zero, a SOCKS4a host name and its zero, which the connector resolves. <c>05</c>, a SOCKS5
/// greeting, is answered <c>05 FF</c> and closed, since this listener serves SOCKS4 only
/// (decision 5). Any other first byte closes the connection without a reply.
/// </para>
/// <para>
/// A <c>CONNECT</c> (<c>CD=01</c>) whose origin the connector opens is answered
/// <c>00 5A 0000 00000000</c>, and bytes are then relayed both ways unread until both sides
/// close, with any bytes curl sent after the request forwarded first. Every refusal is
/// <c>00 5B 0000 00000000</c> and closes the connection: another command (<c>BIND</c> included),
/// an empty SOCKS4a host name, the address <c>0.0.0.0</c>, port 0, any
/// <see cref="OriginConnectionFailure"/>, or a user ID or host name longer than 255 bytes, which
/// is refused without reading further. A user ID that is not empty is logged as
/// <c>SOCKS4 user ID &lt;id&gt;</c> and never checked; <c>5C</c> and <c>5D</c> are never sent.
/// </para>
/// <para>
/// The request must be complete within <see cref="ExchangeLimits.HeadTimeout"/> from the first
/// byte, and the origin must accept within the same timeout. A request that does not arrive in
/// time, or that curl ends early, closes the connection without a reply.
/// </para>
/// </remarks>
public sealed class SocksProtocolServer : IConnectionProtocolServer
{
    private readonly IOriginConnector originConnector;

    /// <summary>
    /// Creates the SOCKS server.
    /// </summary>
    /// <param name="originConnector">How origins are reached: the composition root's, which applies the allowed-origin rule.</param>
    public SocksProtocolServer(IOriginConnector originConnector)
    {
        ArgumentNullException.ThrowIfNull(originConnector);

        this.originConnector = originConnector;
    }

    /// <summary>
    /// The SOCKS listen URL's schemes this server answers: <c>socks4</c> and <c>socks4a</c>,
    /// which serve SOCKS4 and SOCKS4a alike (ADR-0121 decision 5).
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["socks4", "socks4a"]);

    /// <summary>
    /// Answers the request on <paramref name="connection"/> and relays until both sides close, or
    /// refuses it and closes.
    /// </summary>
    /// <param name="connection">curl's accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await new SocksExchange(connection, context, originConnector).ServeAsync();
        }
        catch (IOException)
        {
            context.Log.Note("A connection failed mid-exchange; the exchange ended.");
        }
    }
}
