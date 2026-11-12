using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// What <c>--dns-transport</c> does to the <c>dns</c> listen URLs (ADR-0092 decision 5 and Amendment 1):
/// each one becomes one listen URL per transport that cannot share a listener, <c>udp</c> and <c>tcp</c>
/// together on one (the shared-port pair), <c>tls</c> on its own, TLS from the first byte, on the
/// port the listen URL gave or on 853 when it gave none.
/// </summary>
public static class DnsTransportComposition
{
    private const string DnsScheme = "dns";
    private const string Udp = "udp";
    private const string Tcp = "tcp";
    private const string Tls = "tls";
    private const int DnsOverTlsPort = 853;

    /// <summary>
    /// Finds a <c>dns</c> listen URL given with a non-zero port that would bind TCP and TLS on that one port.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns>The text after <c>surl: </c>, or <see langword="null"/> when no listen URL clashes.</returns>
    public static string? FindRefusal(SurlCommandLine commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        if (!commandLine.DnsTransports.Contains(Tcp) || !commandLine.DnsTransports.Contains(Tls))
        {
            return null;
        }

        var clashing = commandLine.DnsListenUrlsWithPort.FirstOrDefault(listenUrl => listenUrl.Port != 0);
        return clashing is null ? null : $"--dns-transport {Tcp} and {Tls} cannot share port {clashing.Port}";
    }

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
            var port = commandLine.DnsListenUrlsWithPort.Contains(listenUrl) ? listenUrl.Port : DnsOverTlsPort;
            yield return listenUrl with { Port = port, IsImplicitTls = true, OnlyTransport = ListenTransport.Connections };
        }
    }

    private static ListenTransport? OnlyTransportOf(bool datagrams, bool connections) =>
        datagrams && connections ? null : datagrams ? ListenTransport.Datagrams : ListenTransport.Connections;
}
