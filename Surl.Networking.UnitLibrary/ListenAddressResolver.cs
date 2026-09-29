using System.Net;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// Chooses the addresses a listen URL's host names (ADR-0004, section 6): an IP literal is
/// that one address; a host name is every distinct address it resolves to, in the order the
/// resolver returned them.
/// </summary>
internal sealed class ListenAddressResolver
{
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> resolveHostName;

    /// <summary>
    /// Creates a resolver that looks host names up with <paramref name="resolveHostName"/>.
    /// </summary>
    /// <param name="resolveHostName">Resolves a host name; <c>Dns.GetHostAddressesAsync</c> in production.</param>
    public ListenAddressResolver(Func<string, CancellationToken, Task<IPAddress[]>> resolveHostName)
    {
        ArgumentNullException.ThrowIfNull(resolveHostName);

        this.resolveHostName = resolveHostName;
    }

    /// <summary>
    /// Returns every address <paramref name="listenUrl"/>'s host names.
    /// </summary>
    /// <param name="listenUrl">The listen URL whose host is resolved.</param>
    /// <param name="cancellationToken">Cuts the resolution off.</param>
    /// <returns>At least one address, each once.</returns>
    /// <exception cref="ListenerBindException">
    /// The host name did not resolve, or resolved to no address
    /// (<see cref="ListenerBindFailure.HostNotFound"/>).
    /// </exception>
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(ListenUrl listenUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(listenUrl);

        if (IPAddress.TryParse(listenUrl.Host, out var literal))
        {
            return [literal];
        }

        var addresses = (await ResolveHostNameAsync(listenUrl, cancellationToken)).Distinct().ToArray();

        return addresses.Length > 0
            ? addresses
            : throw new ListenerBindException(listenUrl, null, ListenerBindFailure.HostNotFound, null);
    }

    private async Task<IPAddress[]> ResolveHostNameAsync(ListenUrl listenUrl, CancellationToken cancellationToken)
    {
        try
        {
            return await resolveHostName(listenUrl.Host, cancellationToken);
        }
        catch (SocketException exception)
        {
            // Every resolver failure means the name gave no address to bind, whether the
            // resolver said "no such host" or "try again" (ADR-0004, section 6).
            throw new ListenerBindException(listenUrl, null, ListenerBindFailure.HostNotFound, exception);
        }
    }
}
