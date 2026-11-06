using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Imap;

/// <summary>
/// The listen URL as the instance reports it - the form a fault rule's <c>listener</c> is matched
/// against (ADR-0124, decision 4): the bound port, and an IPv6 literal host bracketed with a zone
/// written as <c>%25&lt;zone&gt;</c>, as the serving engine writes it.
/// </summary>
internal static class ReportedListenUrl
{
    /// <summary>
    /// Formats <paramref name="listenUrl"/> as the instance reports it.
    /// </summary>
    /// <param name="listenUrl">The exchange's listen URL.</param>
    /// <returns><c>&lt;scheme&gt;://&lt;host&gt;:&lt;port&gt;/</c>.</returns>
    public static string Format(ListenUrl listenUrl)
    {
        var host = listenUrl.Host.Contains(':', StringComparison.Ordinal)
            ? "[" + listenUrl.Host.Replace("%", "%25", StringComparison.Ordinal) + "]"
            : listenUrl.Host;

        return $"{listenUrl.Scheme}://{host}:{listenUrl.BoundPort ?? listenUrl.Port}/";
    }
}
