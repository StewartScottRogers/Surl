using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// The SOCKS server (ADR-0121 decisions 1 to 4 and 6): the proxy curl's <c>--socks4</c>,
/// <c>--socks4a</c>, <c>--socks5</c>, <c>--socks5-hostname</c> and <c>-x</c>/<c>--proxy</c> with a
/// <c>socks4://</c>, <c>socks4a://</c>, <c>socks5://</c> or <c>socks5h://</c> URL talk to. It
/// connects each request through the injected <see cref="IOriginConnector"/> and relays bytes
/// both ways.
/// </summary>
/// <remarks>
/// <para>
/// The first byte picks the version, and the listen URL's scheme says which version the listener
/// serves (decision 5): <c>socks4</c> and <c>socks4a</c> serve SOCKS4 and SOCKS4a and answer a
/// SOCKS5 greeting <c>05 FF</c>; <c>socks5</c> and <c>socks5h</c> serve SOCKS5 with every address
/// type and answer a SOCKS4 request <c>00 5B</c>. Either refusal closes the connection. Any other
/// first byte closes the connection without a reply.
/// </para>
/// <para>
/// SOCKS4: <c>CD</c>, <c>DSTPORT</c>, <c>DSTIP</c>, a user ID and its zero, and, when <c>DSTIP</c>
/// is <c>0.0.0.x</c> with <c>x</c> not zero, a SOCKS4a host name and its zero, which the connector
/// resolves. A <c>CONNECT</c> (<c>CD=01</c>) whose origin the connector opens is answered
/// <c>00 5A 0000 00000000</c>. Every refusal is <c>00 5B 0000 00000000</c> and closes the
/// connection: another command (<c>BIND</c> included), an empty SOCKS4a host name, the address
/// <c>0.0.0.0</c>, port 0, any <see cref="OriginConnectionFailure"/>, a user ID or host name
/// longer than 255 bytes (refused without reading further), or proxy accounts on the listener
/// without <c>--allow-anonymous</c>. A user ID that is not empty is logged as
/// <c>SOCKS4 user ID &lt;id&gt;</c> and never checked; <c>5C</c> and <c>5D</c> are never sent.
/// </para>
/// <para>
/// SOCKS5 (RFC 1928): the greeting's methods select <c>00</c> when the listener has no proxy
/// accounts, or when it has them and the policy accepts an anonymous login unchecked
/// (<c>--allow-anonymous</c>); otherwise <c>02</c>, RFC 1929 username and password, when offered
/// and the listener has accounts; otherwise <c>05 FF</c> and close. GSS-API (<c>01</c>) is never
/// selected (BL-765). The RFC 1929 login is judged by the policy, which refuses a clear password
/// without <c>--allow-plaintext-auth</c> and delays a wrong one; it is answered <c>01 00</c>, or
/// <c>01 01</c> and close. The request's <c>ATYP</c> <c>01</c>, <c>03</c> (a domain name, which
/// the connector resolves) and <c>04</c> are each connected; success is
/// <c>05 00 00</c> with the origin connection's local address and port. Every failure reply is
/// <c>05 REP 00 01 00000000 0000</c> and closes the connection: <c>07</c> for any command but
/// <c>CONNECT</c> (<c>BIND</c> and <c>UDP ASSOCIATE</c> included), <c>08</c> for any other
/// <c>ATYP</c> (sent without reading further), <c>04</c> for an empty domain name, <c>02</c> for
/// <c>0.0.0.0</c>, <c>::</c> or port 0, and for an <see cref="OriginConnectionFailure"/>
/// <c>02</c> NotAllowed, <c>03</c> Unreachable, <c>04</c> HostNotFound, <c>05</c> Refused,
/// <c>06</c> TimedOut, <c>01</c> anything else. A request or login whose version is wrong, or that
/// curl ends early, closes the connection without a reply. The log never holds a password.
/// </para>
/// <para>
/// After success, bytes are relayed both ways unread until both sides close, with any bytes curl
/// sent after the request forwarded first. The whole handshake - greeting, login and request - must
/// be complete within <see cref="ExchangeLimits.HeadTimeout"/> from the first byte, and the origin
/// must accept within the same timeout. A handshake that does not arrive in time closes the
/// connection without a reply.
/// </para>
/// </remarks>
public sealed class SocksProtocolServer : IConnectionProtocolServer
{
    private readonly IOriginConnector originConnector;
    private readonly IAuthenticationPolicy? proxyAccounts;

    /// <summary>
    /// Creates the SOCKS server for a listener with no proxy accounts: every client may use it
    /// without logging in.
    /// </summary>
    /// <param name="originConnector">How origins are reached: the composition root's, which applies the allowed-origin rule.</param>
    public SocksProtocolServer(IOriginConnector originConnector)
        : this(originConnector, null)
    {
    }

    /// <summary>
    /// Creates the SOCKS server.
    /// </summary>
    /// <param name="originConnector">How origins are reached: the composition root's, which applies the allowed-origin rule.</param>
    /// <param name="proxyAccounts">
    /// The proxy accounts <c>-U</c>/<c>--proxy-user</c> and <c>--proxy-user-file</c> name, as the
    /// policy that judges RFC 1929 logins (ADR-0121 decision 4); <see langword="null"/> when the
    /// listener has none.
    /// </param>
    public SocksProtocolServer(IOriginConnector originConnector, IAuthenticationPolicy? proxyAccounts)
    {
        ArgumentNullException.ThrowIfNull(originConnector);

        this.originConnector = originConnector;
        this.proxyAccounts = proxyAccounts;
    }

    /// <summary>
    /// The SOCKS listen URL's schemes this server answers: <c>socks4</c> and <c>socks4a</c>, which
    /// serve SOCKS4 and SOCKS4a alike, and <c>socks5</c> and <c>socks5h</c>, which serve SOCKS5
    /// alike (ADR-0121 decision 5).
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["socks4", "socks4a", "socks5", "socks5h"]);

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
            await new SocksExchange(connection, context, originConnector, proxyAccounts).ServeAsync();
        }
        catch (IOException)
        {
            context.Log.Note("A connection failed mid-exchange; the exchange ended.");
        }
    }
}
