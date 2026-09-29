using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Surl.Conformance;

/// <summary>
/// Finds the pinned upstream curl build to run on a platform and refuses any curl whose
/// SHA-256 is not pinned - the C# counterpart of <c>Assert-PinnedUpstreamCurl</c> in
/// <c>Record-CurlExchange.ps1</c>, so conformance tests run only upstream curl (ADR-0003).
/// </summary>
/// <remarks>
/// A curl is identified by the hash of its file and never by its name or its
/// <c>--version</c>: the Curl port reports upstream's version number on purpose.
/// </remarks>
/// <param name="fileAccess">How the locator checks for and reads a build's file.</param>
public sealed class UpstreamCurlLocator(IUpstreamCurlFileAccess fileAccess)
{
    /// <summary>
    /// Gets the portable runtime identifier of the running platform, such as <c>win-x64</c>,
    /// <c>linux-x64</c> or <c>osx-arm64</c>, in the form
    /// <see cref="PinnedUpstreamCurlBuild.Platform"/> uses.
    /// </summary>
    /// <remarks>
    /// Built from the operating system and architecture rather than read from
    /// <see cref="RuntimeInformation.RuntimeIdentifier"/>, which on a source-built .NET names
    /// the distribution (<c>ubuntu.24.04-x64</c>) and would never match a <c>linux-x64</c> pin.
    /// </remarks>
    public static string CurrentPlatform => PlatformName(RuntimeInformation.IsOSPlatform, RuntimeInformation.OSArchitecture);

    /// <summary>
    /// Finds the first build of <paramref name="platform"/> and <paramref name="role"/> whose
    /// file exists at its default path and hashes to its pin.
    /// </summary>
    /// <param name="pins">The builds <c>UpstreamCurlBuilds.json</c> pins.</param>
    /// <param name="platform">The platform to find a build for, such as <c>win-x64</c>.</param>
    /// <param name="role">The role the build must have; a supplementary build is returned only when asked for.</param>
    /// <returns>
    /// The verified build, or a location saying that no build is pinned for the platform and
    /// role, or that none of the pinned builds' files exists.
    /// </returns>
    /// <exception cref="UnpinnedUpstreamCurlException">
    /// A pinned build's file exists but hashes to something else, and no other pinned build of
    /// the platform and role verifies.
    /// </exception>
    public UpstreamCurlLocation Locate(
        IReadOnlyList<PinnedUpstreamCurlBuild> pins,
        string platform,
        UpstreamCurlBuildRole role = UpstreamCurlBuildRole.Reference)
    {
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(platform);

        var candidates = pins.Where(pin => pin.Platform == platform && pin.Role == role).ToList();

        return candidates.Count == 0
            ? UpstreamCurlLocation.NoPinnedBuild(platform, role)
            : VerifyFirstPresent(candidates);
    }

    /// <summary>
    /// Checks each candidate whose file exists, in order, and returns the first that hashes to
    /// its pin.
    /// </summary>
    private UpstreamCurlLocation VerifyFirstPresent(List<PinnedUpstreamCurlBuild> candidates)
    {
        UnpinnedUpstreamCurlException? refusal = null;

        foreach (var candidate in candidates.Where(candidate => fileAccess.FileExists(candidate.DefaultPath)))
        {
            var sha256 = HashFile(candidate.DefaultPath);

            if (MatchesPin(sha256, candidate))
            {
                return UpstreamCurlLocation.Found(candidate);
            }

            refusal ??= new UnpinnedUpstreamCurlException(candidate.DefaultPath, sha256);
        }

        return refusal is null ? UpstreamCurlLocation.FileAbsent(candidates[0]) : throw refusal;
    }

    /// <summary>
    /// Checks that the curl at <paramref name="path"/> is one of the pinned builds, whatever
    /// its platform or role, as <c>Assert-PinnedUpstreamCurl</c> does for a curl named on
    /// <c>Record-CurlExchange.ps1</c>'s command line.
    /// </summary>
    /// <param name="pins">The builds <c>UpstreamCurlBuilds.json</c> pins.</param>
    /// <param name="path">The path of the curl executable.</param>
    /// <returns>The first pinned build whose SHA-256 the file matches.</returns>
    /// <exception cref="UnpinnedUpstreamCurlException">The file's SHA-256 matches no pin.</exception>
    /// <exception cref="FileNotFoundException">No file exists at <paramref name="path"/>.</exception>
    public PinnedUpstreamCurlBuild RequirePinned(IReadOnlyList<PinnedUpstreamCurlBuild> pins, string path)
    {
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(path);

        if (!fileAccess.FileExists(path))
        {
            throw new FileNotFoundException($"The curl to run, {path}, was not found.", path);
        }

        var sha256 = HashFile(path);

        return pins.FirstOrDefault(pin => MatchesPin(sha256, pin))
            ?? throw new UnpinnedUpstreamCurlException(path, sha256);
    }

    /// <summary>
    /// Names a platform the way a portable runtime identifier does: <c>win</c>, <c>osx</c> or
    /// <c>linux</c> (<c>unknown</c> for any other system, which no pin names), a hyphen, and
    /// the architecture in lower case.
    /// </summary>
    internal static string PlatformName(Func<OSPlatform, bool> isOSPlatform, Architecture architecture)
    {
        var operatingSystem = isOSPlatform(OSPlatform.Windows) ? "win"
            : isOSPlatform(OSPlatform.OSX) ? "osx"
            : isOSPlatform(OSPlatform.Linux) ? "linux"
            : "unknown";

        return $"{operatingSystem}-{architecture.ToString().ToLowerInvariant()}";
    }

    private static bool MatchesPin(string sha256, PinnedUpstreamCurlBuild pin) =>
        string.Equals(sha256, pin.Sha256, StringComparison.OrdinalIgnoreCase);

    private string HashFile(string path)
    {
        using var stream = fileAccess.OpenRead(path);

        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
