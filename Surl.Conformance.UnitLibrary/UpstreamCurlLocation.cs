namespace Surl.Conformance;

/// <summary>
/// What <see cref="UpstreamCurlLocator"/> found: either a verified pinned build, or
/// the reason none is available on this machine.
/// </summary>
public sealed class UpstreamCurlLocation
{
    private UpstreamCurlLocation(PinnedUpstreamCurlBuild? build, UpstreamCurlUnavailability unavailability, string message)
    {
        Build = build;
        Unavailability = unavailability;
        Message = message;
    }

    /// <summary>
    /// Gets the verified pinned build, or <see langword="null"/> when none is available.
    /// </summary>
    public PinnedUpstreamCurlBuild? Build { get; }

    /// <summary>
    /// Gets why no build is available, or <see cref="UpstreamCurlUnavailability.None"/> when one is.
    /// </summary>
    public UpstreamCurlUnavailability Unavailability { get; }

    /// <summary>
    /// Gets one sentence describing the outcome, fit for an inconclusive test's message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets a value indicating whether a verified pinned build was found.
    /// </summary>
    public bool IsAvailable => Build is not null;

    /// <summary>
    /// Creates the result for a build whose file exists and matches its pin.
    /// </summary>
    /// <param name="build">The verified build.</param>
    /// <returns>An available location.</returns>
    public static UpstreamCurlLocation Found(PinnedUpstreamCurlBuild build) =>
        new(build, UpstreamCurlUnavailability.None, $"{build.DefaultPath} is the pinned {RoleName(build.Role)} upstream curl build for {build.Platform}.");

    /// <summary>
    /// Creates the result for a platform and role with no pinned build.
    /// </summary>
    /// <param name="platform">The platform asked for.</param>
    /// <param name="role">The role asked for.</param>
    /// <returns>An unavailable location.</returns>
    public static UpstreamCurlLocation NoPinnedBuild(string platform, UpstreamCurlBuildRole role) =>
        new(null, UpstreamCurlUnavailability.NoPinnedBuildForPlatform, $"UpstreamCurlBuilds.json pins no {RoleName(role)} upstream curl build for {platform}.");

    /// <summary>
    /// Creates the result for a platform with no pinned build that supports a protocol.
    /// </summary>
    /// <param name="platform">The platform asked for.</param>
    /// <param name="protocol">The protocol asked for.</param>
    /// <returns>An unavailable location.</returns>
    public static UpstreamCurlLocation NoPinnedBuildForProtocol(string platform, string protocol) =>
        new(null, UpstreamCurlUnavailability.NoPinnedBuildForPlatform, $"UpstreamCurlBuilds.json pins no upstream curl build for {platform} that supports {protocol}.");

    /// <summary>
    /// Creates the result for a pinned build whose file is not on this machine.
    /// </summary>
    /// <param name="build">The first pinned build that was looked for.</param>
    /// <returns>An unavailable location.</returns>
    public static UpstreamCurlLocation FileAbsent(PinnedUpstreamCurlBuild build) =>
        new(null, UpstreamCurlUnavailability.PinnedBuildFileAbsent, $"The pinned {RoleName(build.Role)} upstream curl build for {build.Platform} is not installed: {build.DefaultPath} does not exist.");

    private static string RoleName(UpstreamCurlBuildRole role) =>
        role == UpstreamCurlBuildRole.Reference ? "reference" : "supplementary";
}
