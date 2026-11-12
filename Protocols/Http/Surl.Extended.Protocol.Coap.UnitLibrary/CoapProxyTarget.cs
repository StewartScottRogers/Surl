using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The local request a proxy request stands for when it names the listener itself (RFC 7252
/// section 5.7.2; ADR-0097 decision 3): its scheme, host and port are the listener's.
/// </summary>
/// <param name="Path">The Uri-Path segments of the request.</param>
/// <param name="Query">The Uri-Query arguments of the request.</param>
internal sealed record CoapProxyTarget(IReadOnlyList<string> Path, IReadOnlyList<string> Query)
{
    private const int CoapPort = 5683;

    private const int CoapsPort = 5684;

    /// <summary>
    /// Works out whether <paramref name="options"/> names <paramref name="listener"/>.
    /// </summary>
    /// <param name="options">A request's options, with Proxy-Uri or Proxy-Scheme among them.</param>
    /// <param name="listener">The listen URL that accepted the exchange.</param>
    /// <returns>The local request, or null when another scheme, host or port is named or the URI is unusable.</returns>
    public static CoapProxyTarget? TryResolve(CoapRequestOptions options, ListenUrl listener) =>
        options.ProxyUri is { } proxyUri
            ? ResolveProxyUri(proxyUri, listener)
            : ResolveProxyScheme(options, listener);

    private static CoapProxyTarget? ResolveProxyUri(string proxyUri, ListenUrl listener)
    {
        if (!Uri.TryCreate(proxyUri, UriKind.Absolute, out var uri)
            || uri.Fragment.Length != 0
            || !NamesListener(uri.Scheme, uri.Host.Trim('[', ']'), uri.IsDefaultPort ? DefaultPortOf(uri.Scheme) : uri.Port, listener))
        {
            return null;
        }

        var path = uri.AbsolutePath == "/" ? [] : uri.AbsolutePath[1..].Split('/').Select(Uri.UnescapeDataString);
        var query = uri.Query.Length < 2 ? [] : uri.Query[1..].Split('&').Select(Uri.UnescapeDataString);

        return new CoapProxyTarget([.. path], [.. query]);
    }

    // With no Uri-Host the request is aimed at the endpoint it reached, which is the listener.
    private static CoapProxyTarget? ResolveProxyScheme(CoapRequestOptions options, ListenUrl listener)
    {
        var scheme = options.ProxyScheme!;
        var host = options.UriHost ?? listener.Host;
        var port = options.UriPort is { } uriPort ? (int)uriPort : options.UriHost is null ? listener.BoundPort ?? listener.Port : DefaultPortOf(scheme);

        return NamesListener(scheme, host, port, listener) ? new CoapProxyTarget(options.UriPath, options.UriQuery) : null;
    }

    private static int DefaultPortOf(string scheme) =>
        string.Equals(scheme, "coaps", StringComparison.OrdinalIgnoreCase) ? CoapsPort : CoapPort;

    private static bool NamesListener(string scheme, string host, int port, ListenUrl listener) =>
        string.Equals(scheme, listener.Scheme, StringComparison.OrdinalIgnoreCase)
        && port == (listener.BoundPort ?? listener.Port)
        && IsListenerHost(host, listener.Host);

    private static bool IsListenerHost(string host, string listenerHost) =>
        string.Equals(host, listenerHost, StringComparison.OrdinalIgnoreCase)
        || (IsLoopback(host) && IsLoopback(listenerHost));

    private static bool IsLoopback(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
}
