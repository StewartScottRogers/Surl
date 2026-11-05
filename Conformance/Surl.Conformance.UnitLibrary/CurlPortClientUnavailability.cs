namespace Surl.Conformance;

/// <summary>
/// Why <see cref="CurlPortClientLocator"/> found no pinned Curl port client to run.
/// </summary>
public enum CurlPortClientUnavailability
{
    /// <summary>
    /// A pinned client was found; nothing is unavailable.
    /// </summary>
    None,

    /// <summary>
    /// <c>CurlPortBuilds.json</c> pins no client for the platform, or pins one only at a path
    /// <c>UpstreamCurlBuilds.json</c> lists, which the port's locator never returns.
    /// </summary>
    NoPinnedClientForPlatform,

    /// <summary>
    /// A client is pinned for the platform, but no file exists at its path.
    /// </summary>
    PinnedClientFileAbsent,
}
