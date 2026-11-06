using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// The <c>WWW-Authenticate</c> values the registry sends (Token "How to authenticate"; RFC 6750,
/// section 3; ADR-0115, decisions 4.1, 4.2 and 4.4).
/// </summary>
internal static class OciChallenge
{
    /// <summary>
    /// The <c>service</c> every challenge names and every token request must name, if any.
    /// </summary>
    public const string Service = "surl";

    /// <summary>
    /// The challenge the token endpoint answers a missing or refused login with (RFC 7617).
    /// </summary>
    public const string Basic = "Basic realm=\"surl\", charset=\"UTF-8\"";

    /// <summary>
    /// The Bearer challenge: the realm, the service, the scope the request needs when it names
    /// one, and the RFC 6750 error when there is one.
    /// </summary>
    /// <param name="context">The exchange: the listen URL and local endpoint the realm is taken from.</param>
    /// <param name="isEncrypted">Whether the connection runs TLS, so the realm is <c>https</c>.</param>
    /// <param name="scope">The scope the request needs, or <see langword="null"/> for <c>/v2/</c>.</param>
    /// <param name="error">The RFC 6750 error code, or <see langword="null"/> for none.</param>
    /// <returns>The field's value.</returns>
    public static string Bearer(ExchangeContext context, bool isEncrypted, string? scope, string? error)
    {
        var value = $"Bearer realm=\"{Realm(context, isEncrypted)}\",service=\"{Service}\"";
        if (scope is not null)
        {
            value += $",scope=\"{scope}\"";
        }

        return error is null ? value : value + $",error=\"{error}\"";
    }

    /// <summary>
    /// The token endpoint's URL (decision 4.2): <c>https</c>, or <c>http</c> on a plain
    /// connection; the listen URL's host when it is a DNS name, else the local address the
    /// connection arrived on; the bound port. Never the request's <c>Host</c> field, which would
    /// let a peer send another client's password elsewhere.
    /// </summary>
    /// <param name="context">The exchange.</param>
    /// <param name="isEncrypted">Whether the connection runs TLS.</param>
    /// <returns>The realm.</returns>
    public static string Realm(ExchangeContext context, bool isEncrypted)
    {
        var listenUrl = context.ListenUrl;
        var port = (listenUrl.BoundPort ?? listenUrl.Port).ToString(CultureInfo.InvariantCulture);

        return $"{(isEncrypted ? "https" : "http")}://{Host(listenUrl.Host, context.LocalEndPoint)}:{port}/token";
    }

    private static string Host(string listenHost, EndPoint localEndPoint)
    {
        if (!IPAddress.TryParse(listenHost, out _) || localEndPoint is not IPEndPoint local)
        {
            return listenHost;
        }

        var address = local.Address.IsIPv4MappedToIPv6 ? local.Address.MapToIPv4() : local.Address;

        return address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{address}]" : address.ToString();
    }
}
