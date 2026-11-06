using System.Net;
using System.Net.Sockets;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The path-style form ("Naming and Referencing Containers, Blobs, and Metadata": emulator and
/// IP-style addressing, <c>http://&lt;ip&gt;:&lt;port&gt;/&lt;account&gt;/&lt;container&gt;/&lt;blob&gt;</c>;
/// ADR-0109, decision 2): when the <c>Host</c> name, its port removed, is an IP literal or
/// <c>localhost</c>, and the path's first segment, percent-decoded, is the name of a named account,
/// that segment is the account and is removed before the container is read. With no named
/// accounts the form never applies.
/// </summary>
/// <param name="namesAccount">Whether a name is a named account's (<c>IAuthenticationPolicy.NamesAccount</c>).</param>
internal sealed class AzurePathStyle(Func<string, bool> namesAccount)
{
    /// <summary>
    /// Reads a request's target, removing the path-style account segment when there is one.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>
    /// The target the server reads the container and blob from, and the account the path-style
    /// segment named (<see langword="null"/> in the account-host form).
    /// </returns>
    public (AzureBlobTarget Target, string? Account) Read(HttpRequest request)
    {
        var target = AzureBlobTarget.Parse(request.Target);
        if (target.RawContainer.Length == 0 || !IsAddressedByIpOrLocalhost(AzureRequestFields.Value(request.Fields, "Host")))
        {
            return (target, null);
        }

        var account = Uri.UnescapeDataString(target.RawContainer);
        if (!namesAccount(account))
        {
            return (target, null);
        }

        var queryStart = request.Target.IndexOf('?');
        var query = queryStart < 0 ? string.Empty : request.Target[queryStart..];

        return (AzureBlobTarget.Parse("/" + target.RawBlob + query), account);
    }

    /// <summary>
    /// Whether a <c>Host</c> field names an IP literal (<c>127.0.0.1</c>, <c>[::1]</c>) or
    /// <c>localhost</c>, with or without a port.
    /// </summary>
    /// <param name="host">The field's value, or <see langword="null"/> without one.</param>
    /// <returns><see langword="true"/> for an IP literal or <c>localhost</c>.</returns>
    public static bool IsAddressedByIpOrLocalhost(string? host)
    {
        if (host is null)
        {
            return false;
        }

        if (host.StartsWith('['))
        {
            var close = host.IndexOf(']');

            return close > 0 && IPAddress.TryParse(host[1..close], out var address) && address.AddressFamily == AddressFamily.InterNetworkV6;
        }

        var name = host.Split(':')[0];

        return string.Equals(name, "localhost", StringComparison.OrdinalIgnoreCase) || IsDottedQuad(name);
    }

    // Four dotted decimal parts, so a one-label name of digits is not read as an address.
    private static bool IsDottedQuad(string name) =>
        name.Count(character => character == '.') == 3
            && IPAddress.TryParse(name, out var address)
            && address.AddressFamily == AddressFamily.InterNetwork;
}
