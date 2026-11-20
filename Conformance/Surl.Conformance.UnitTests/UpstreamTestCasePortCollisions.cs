namespace Surl.Conformance;

/// <summary>
/// Finds listeners of different schemes on different hosts that the operating system numbered with the
/// same port, which sends a client that tries <c>::1</c> before <c>127.0.0.1</c> to the wrong server.
/// </summary>
internal static class UpstreamTestCasePortCollisions
{
    /// <summary>
    /// Whether two bound listeners, keyed "<c>scheme host</c>", share a nonzero port while differing in both
    /// scheme and host. Two listeners of one scheme (the IPv4 and IPv6 halves of one server) may share one.
    /// </summary>
    /// <param name="ports">The bound port of each listener, by "scheme host".</param>
    public static bool AnyAcrossHosts(IReadOnlyDictionary<string, int> ports)
    {
        ArgumentNullException.ThrowIfNull(ports);

        return ports
            .Where(entry => entry.Value != 0)
            .GroupBy(entry => entry.Value)
            .Any(group => group.Select(entry => HostOf(entry.Key)).Distinct(StringComparer.Ordinal).Count() > 1
                && group.Select(entry => SchemeOf(entry.Key)).Distinct(StringComparer.Ordinal).Count() > 1);
    }

    private static string SchemeOf(string key) => key[..key.IndexOf(' ', StringComparison.Ordinal)];

    private static string HostOf(string key) => key[(key.IndexOf(' ', StringComparison.Ordinal) + 1)..];
}
