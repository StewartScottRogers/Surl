namespace Surl.Conformance;

/// <summary>
/// Why <see cref="UpstreamCurlLocator"/> found no pinned upstream curl build to run.
/// </summary>
public enum UpstreamCurlUnavailability
{
    /// <summary>
    /// A pinned build was found; nothing is unavailable.
    /// </summary>
    None,

    /// <summary>
    /// <c>UpstreamCurlBuilds.json</c> pins no build of the asked-for role for the platform.
    /// </summary>
    NoPinnedBuildForPlatform,

    /// <summary>
    /// A build is pinned for the platform and role, but no file exists at its default path.
    /// </summary>
    PinnedBuildFileAbsent,
}
