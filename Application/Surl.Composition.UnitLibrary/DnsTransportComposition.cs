using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// What <c>--dns-transport</c> does to the <c>dns</c> listen URLs (ADR-0092 decision 5 and Amendment 1):
/// each one becomes one listen URL per transport that cannot share a listener, <c>udp</c> and <c>tcp</c>
/// together on one (the shared-port pair), <c>tls</c> on its own, TLS from the first byte, <c>quic</c> on its own datagram listener, on the
/// port the listen URL gave or on 853 when it gave none.
/// </summary>
public static class DnsTransportComposition
{
    private const string DnsScheme = "dns";
    private const string Udp = "udp";
    private const string Tcp = "tcp";
    private const string Tls = "tls";
    private const string Quic = "quic";
    private const string Https = "https";
    private const int DnsPort = 53;
    private const int DnsOverTlsPort = 853;
    private const int DnsOverHttpsPort = 443;

    /// <summary>
    /// Finds a <c>dns</c> listen URL given with a non-zero port that would bind TCP and TLS, or UDP and QUIC, on that one port, and QUIC on port 53 (RFC 9250 section 4.1.1).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns>The text after <c>surl: </c>, or <see langword="null"/> when no listen URL clashes.</returns>
    public static string? FindRefusal(SurlCommandLine commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        return FindShareRefusal(commandLine, Tcp, Tls) ?? FindShareRefusal(commandLine, Udp, Quic)
            ?? FindShareRefusal(commandLine, Tcp, Https) ?? FindShareRefusal(commandLine, Tls, Https) ?? FindQuicOnDnsPortRefusal(commandLine);
    }

    private static string? FindShareRefusal(SurlCommandLine commandLine, string first, string second)
    {
        if (!commandLine.DnsTransports.Contains(first) || !commandLine.DnsTransports.Contains(second))
        {
            return null;
        }

        var clashing = commandLine.DnsListenUrlsWithPort.FirstOrDefault(listenUrl => listenUrl.Port != 0);
        return clashing is null ? null : $"--dns-transport {first} and {second} cannot share port {clashing.Port}";
    }

    private static string? FindQuicOnDnsPortRefusal(SurlCommandLine commandLine) =>
        commandLine.DnsTransports.Contains(Quic) && commandLine.DnsListenUrlsWithPort.Any(listenUrl => listenUrl.Port == DnsPort)
            ? $"--dns-transport {Quic} cannot use port {DnsPort}"
            : null;

    /// <summary>
    /// Replaces each <c>dns</c> listen URL of <paramref name="commandLine"/> with the listen URLs of its
    /// transports; the default <c>udp,tcp</c> leaves it as it is.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns>The command line with its <c>dns</c> listen URLs expanded, or itself when none changes.</returns>
    public static SurlCommandLine ExpandDnsListenUrls(SurlCommandLine commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        return commandLine with { ListenUrls = [.. commandLine.ListenUrls.SelectMany(listenUrl => Expand(commandLine, listenUrl))] };
    }

    private static IEnumerable<ListenUrl> Expand(SurlCommandLine commandLine, ListenUrl listenUrl)
    {
        if (listenUrl.Scheme != DnsScheme)
        {
            yield return listenUrl;
            yield break;
        }

        var transports = commandLine.DnsTransports;
        var datagrams = transports.Contains(Udp);
        var connections = transports.Contains(Tcp);
        if (datagrams || connections)
        {
            yield return listenUrl with { OnlyTransport = OnlyTransportOf(datagrams, connections) };
        }

        if (transports.Contains(Tls))
        {
            yield return listenUrl with { Port = SecurePortOf(commandLine, listenUrl), IsImplicitTls = true, OnlyTransport = ListenTransport.Connections };
        }

        // DNS over HTTPS is a connection listener with TLS from the first byte, marked so it offers h2 then http/1.1 (RFC 8484).
        if (transports.Contains(Https))
        {
            yield return listenUrl with
            {
                Port = SecurePortOf(commandLine, listenUrl, DnsOverHttpsPort),
                IsImplicitTls = true,
                IsDnsOverHttps = true,
                OnlyTransport = ListenTransport.Connections,
            };
        }

        // DNS over QUIC is the datagram listener with TLS from the first byte: the one place a datagram
        // listen URL is IsImplicitTls, which is how the routing server finds it (RFC 9250).
        if (transports.Contains(Quic))
        {
            yield return listenUrl with { Port = SecurePortOf(commandLine, listenUrl), IsImplicitTls = true, OnlyTransport = ListenTransport.Datagrams };
        }
    }

    private static int SecurePortOf(SurlCommandLine commandLine, ListenUrl listenUrl, int defaultPort = DnsOverTlsPort) =>
        commandLine.DnsListenUrlsWithPort.Contains(listenUrl) ? listenUrl.Port : defaultPort;

    private static ListenTransport? OnlyTransportOf(bool datagrams, bool connections) =>
        datagrams && connections ? null : datagrams ? ListenTransport.Datagrams : ListenTransport.Connections;
}
