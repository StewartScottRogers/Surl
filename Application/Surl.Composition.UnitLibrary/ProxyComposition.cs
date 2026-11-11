using System.Net;
using Surl.Cli;
using Surl.Output;
using Surl.Protocol.Abstractions;
using Surl.Protocol.HttpProxy;

namespace Surl.Composition;

/// <summary>
/// Composes the HTTP and HTTPS proxies from the command line's <c>--proxy</c> listen URLs
/// (ADR-0088 decisions 2, 5, 6 and 7): the allowed-origin rule, the proxy server, and its routing
/// beside the origin servers that answer the same schemes.
/// </summary>
internal static class ProxyComposition
{
    private const string WarningPrefix = "surl: warning: ";

    /// <summary>
    /// The rule the proxy's origin connector applies: loopback only, widened by every
    /// <c>--proxy-allow-origin</c> and narrowed by each proxy listen URL's own end point
    /// (decision 2). A proxy on port 0 has no port to refuse yet; a host that is not an address
    /// refuses its port on every address.
    /// </summary>
    internal static AllowedOriginRule ComposeAllowedOriginRule(SurlCommandLine commandLine)
    {
        var rule = AllowedOriginRule.LoopbackOnly;
        foreach (var origin in commandLine.ProxyAllowedOrigins)
        {
            rule = origin.AdmitsEveryAddress ? AllowedOriginRule.EveryAddress : rule.WithRange(origin.Network, origin.PrefixLength);
        }

        var proxyListenUrls = commandLine.ProxyListenUrls.Concat(commandLine.ListenUrls.Where(listenUrl => listenUrl.Scheme.StartsWith("socks", StringComparison.Ordinal))).Distinct();
        foreach (var listenUrl in proxyListenUrls.Where(listenUrl => listenUrl.Port > 0))
        {
            var address = IPAddress.TryParse(listenUrl.Host, out var parsed) ? parsed : IPAddress.Any;
            rule = rule.WithProxyEndPoint(new IPEndPoint(address, listenUrl.Port));
        }

        return rule;
    }

    /// <summary>
    /// Wraps every server answering <c>http</c> or <c>https</c> so a <c>--proxy</c> listen URL is
    /// served by one <see cref="HttpProxyProtocolServer"/> over the given origin connector and
    /// proxy accounts; the servers are returned as they are when no proxy is named.
    /// </summary>
    internal static IProtocolServer[] RouteProxyListeners(
        IProtocolServer[] servers, SurlCommandLine commandLine, IOriginConnector connector, IAuthenticationPolicy proxyAuthenticationPolicy)
    {
        if (commandLine.ProxyListenUrls.Count == 0)
        {
            return servers;
        }

        var proxy = new HttpProxyProtocolServer(connector, proxyAuthenticationPolicy);
        return [.. servers.Select(server => IsAnsweringHttp(server) ? new ProxyRoutingProtocolServer((IConnectionProtocolServer)server, proxy) : server)];
    }

    /// <summary>Writes the loosening warning <c>--proxy-allow-origin '*'</c> makes, from the info level up.</summary>
    internal static void WriteLooseningWarning(SurlCommandLine commandLine, TextWriter log)
    {
        if (commandLine.LogLevel >= LogLevel.Info && commandLine.ProxyAllowedOrigins.Any(origin => origin.AdmitsEveryAddress))
        {
            log.WriteLine(WarningPrefix + "--proxy-allow-origin: the proxy connects to every origin address, not only loopback");
        }
    }

    private static bool IsAnsweringHttp(IProtocolServer server) =>
        server is IConnectionProtocolServer && server.Schemes.Any(scheme => scheme is "http" or "https");
}
