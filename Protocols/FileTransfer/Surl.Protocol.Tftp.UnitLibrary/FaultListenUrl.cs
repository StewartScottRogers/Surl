using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Tftp;

/// <summary>
/// Writes an exchange's listen URL as a <see cref="FaultPoint.ListenUrl"/> carries it: the form the
/// serving engine reports, <c>scheme://host:port/</c> with the bound port and an IPv6 host in brackets
/// (a zone's <c>%</c> written <c>%25</c>), so a <c>listener</c>-scoped rule matches a command point as
/// it matches the engine's accept point (ADR-0124, decision 4).
/// </summary>
internal static class FaultListenUrl
{
    /// <summary>
    /// The listen URL's text.
    /// </summary>
    /// <param name="listenUrl">The exchange's listen URL.</param>
    /// <returns>The URL, with the bound port when one is set and the asked-for port otherwise.</returns>
    public static string Text(ListenUrl listenUrl)
    {
        var host = listenUrl.Host.Contains(':', StringComparison.Ordinal)
            ? "[" + listenUrl.Host.Replace("%", "%25", StringComparison.Ordinal) + "]"
            : listenUrl.Host;

        return $"{listenUrl.Scheme}://{host}:{listenUrl.BoundPort ?? listenUrl.Port}/";
    }
}
