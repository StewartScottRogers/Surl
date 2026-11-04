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

        var candidates = pins
            .Where(pin => pin.Kind == UpstreamCurlBuildKind.Curl && pin.Platform == platform && pin.Role == role)
            .ToList();

        return candidates.Count == 0
            ? UpstreamCurlLocation.NoPinnedBuild(platform, role)
            : VerifyFirstPresent(candidates);
    }

    /// <summary>
    /// Finds the build of <paramref name="platform"/> to measure <paramref name="protocol"/>
    /// with: the reference build when its protocols include it, and otherwise the first
    /// supplementary build whose protocols include it and whose file exists and hashes to its
    /// pin - the reference build answers every case it can, and a supplementary build only what
    /// it was pinned for (ADR-0017, ADR-0030).
    /// </summary>
    /// <param name="pins">The builds <c>UpstreamCurlBuilds.json</c> pins.</param>
    /// <param name="platform">The platform to find a build for, such as <c>win-x64</c>.</param>
    /// <param name="protocol">The protocol the build must support, as <c>curl --version</c> names it, such as <c>smb</c>; compared case-insensitively.</param>
    /// <returns>
    /// The verified build, or a location saying that no build of the platform supports the
    /// protocol, or that none of the pinned builds' files exists.
    /// </returns>
    /// <exception cref="UnpinnedUpstreamCurlException">
    /// A chosen build's file exists but hashes to something else, and no other pinned build of
    /// the same role that supports the protocol verifies.
    /// </exception>
    public UpstreamCurlLocation LocateForProtocol(
        IReadOnlyList<PinnedUpstreamCurlBuild> pins,
        string platform,
        string protocol)
    {
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(protocol);

        var supporting = pins
            .Where(pin => pin.Kind == UpstreamCurlBuildKind.Curl
                && pin.Platform == platform
                && pin.Protocols.Contains(protocol, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var references = supporting.Where(pin => pin.Role == UpstreamCurlBuildRole.Reference).ToList();
        var candidates = references.Count > 0 ? references : supporting;

        return candidates.Count == 0
            ? UpstreamCurlLocation.NoPinnedBuildForProtocol(platform, protocol)
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

            refusal ??= new UnpinnedUpstreamCurlException(candidate.DefaultPath, sha256, candidate.Kind);
        }

        return refusal is null ? UpstreamCurlLocation.FileAbsent(candidates[0]) : throw refusal;
    }

    /// <summary>
    /// Finds the first libcurl pinned for <paramref name="platform"/> whose file exists at its
    /// default path and hashes to its pin, for a driver of libcurl's API to load (ADR-0071
    /// decision 10). A curl executable's pin is never returned.
    /// </summary>
    /// <param name="pins">The builds <c>UpstreamCurlBuilds.json</c> pins.</param>
    /// <param name="platform">The platform to find a library for, such as <c>win-x64</c>.</param>
    /// <returns>
    /// The verified library, or a location saying that no library is pinned for the platform,
    /// as on Linux and macOS, whose static builds carry none, or that its file does not exist.
    /// </returns>
    /// <exception cref="UnpinnedUpstreamCurlException">
    /// A pinned library's file exists but hashes to something else, and no other pinned library
    /// of the platform verifies.
    /// </exception>
    public UpstreamCurlLocation LocateLibrary(IReadOnlyList<PinnedUpstreamCurlBuild> pins, string platform)
    {
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(platform);

        var candidates = pins
            .Where(pin => pin.Kind == UpstreamCurlBuildKind.Library && pin.Platform == platform)
            .ToList();

        return candidates.Count == 0
            ? UpstreamCurlLocation.NoPinnedLibrary(platform)
            : VerifyFirstPresent(candidates);
    }

    /// <summary>
    /// Checks that the curl at <paramref name="path"/> is one of the pinned curl builds, whatever
    /// its platform or role, as <c>Assert-PinnedUpstreamCurl</c> does for a curl named on
    /// <c>Record-CurlExchange.ps1</c>'s command line. A pinned libcurl is never a curl to run.
    /// </summary>
    /// <param name="pins">The builds <c>UpstreamCurlBuilds.json</c> pins.</param>
    /// <param name="path">The path of the curl executable.</param>
    /// <returns>The first pinned curl build whose SHA-256 the file matches.</returns>
    /// <exception cref="UnpinnedUpstreamCurlException">The file's SHA-256 matches no curl pin.</exception>
    /// <exception cref="FileNotFoundException">No file exists at <paramref name="path"/>.</exception>
    public PinnedUpstreamCurlBuild RequirePinned(IReadOnlyList<PinnedUpstreamCurlBuild> pins, string path) =>
        RequirePinnedOfKind(pins, path, UpstreamCurlBuildKind.Curl);

    /// <summary>
    /// Checks that the libcurl at <paramref name="path"/> is one of the pinned libraries, as
    /// <c>Assert-PinnedUpstreamCurl -Kind library</c> does for the library
    /// <c>Record-CurlExchange.ps1 -Libcurl</c> names. A pinned curl executable is never a
    /// library to load.
    /// </summary>
    /// <param name="pins">The builds <c>UpstreamCurlBuilds.json</c> pins.</param>
    /// <param name="path">The path of the shared library.</param>
    /// <returns>The first pinned library whose SHA-256 the file matches.</returns>
    /// <exception cref="UnpinnedUpstreamCurlException">The file's SHA-256 matches no library pin.</exception>
    /// <exception cref="FileNotFoundException">No file exists at <paramref name="path"/>.</exception>
    public PinnedUpstreamCurlBuild RequirePinnedLibrary(IReadOnlyList<PinnedUpstreamCurlBuild> pins, string path) =>
        RequirePinnedOfKind(pins, path, UpstreamCurlBuildKind.Library);

    private PinnedUpstreamCurlBuild RequirePinnedOfKind(IReadOnlyList<PinnedUpstreamCurlBuild> pins, string path, UpstreamCurlBuildKind kind)
    {
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(path);

        if (!fileAccess.FileExists(path))
        {
            var what = kind == UpstreamCurlBuildKind.Library ? "libcurl to load" : "curl to run";
            throw new FileNotFoundException($"The {what}, {path}, was not found.", path);
        }

        var sha256 = HashFile(path);

        return pins.FirstOrDefault(pin => pin.Kind == kind && MatchesPin(sha256, pin))
            ?? throw new UnpinnedUpstreamCurlException(path, sha256, kind);
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
