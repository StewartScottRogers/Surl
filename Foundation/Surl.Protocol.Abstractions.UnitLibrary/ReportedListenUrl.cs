namespace Surl.Protocol.Abstractions;

/// <summary>
/// The listen URL as the instance reports it - the format of <c>surl</c>'s "Listening on" line
/// (ADR-0007, section 7), of every event that names a listener, and the form a fault rule's
/// <c>listener</c> is matched against (ADR-0124, decision 4): the bound port, and an IPv6 literal
/// host bracketed with a zone written as <c>%25&lt;zone&gt;</c>. A listener on a Unix domain socket
/// binds no port, so its URL carries the port asked for.
/// </summary>
public static class ReportedListenUrl
{
    /// <summary>
    /// Formats <paramref name="listenUrl"/> as the instance reports it.
    /// </summary>
    /// <param name="listenUrl">The listen URL.</param>
    /// <returns><c>&lt;scheme&gt;://&lt;host&gt;:&lt;port&gt;/</c>.</returns>
    public static string Format(ListenUrl listenUrl)
    {
        ArgumentNullException.ThrowIfNull(listenUrl);

        var host = listenUrl.Host.Contains(':', StringComparison.Ordinal)
            ? "[" + listenUrl.Host.Replace("%", "%25", StringComparison.Ordinal) + "]"
            : listenUrl.Host;

        return $"{listenUrl.Scheme}://{host}:{listenUrl.BoundPort ?? listenUrl.Port}/";
    }
}
