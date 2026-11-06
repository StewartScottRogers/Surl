using System.Security.Cryptography;

namespace Surl.Conformance;

/// <summary>
/// Finds the Curl port client <c>CurlPortBuilds.json</c> pins for a platform and refuses any
/// whose SHA-256 is not pinned (ADR-0083 decision 2), for the integration tests of the extended
/// servers only.
/// </summary>
/// <remarks>
/// A separate type from <see cref="UpstreamCurlLocator"/>, so neither can be handed the other's
/// binary: it never returns a path <c>UpstreamCurlBuilds.json</c> lists, and refuses a file whose
/// hash is an upstream pin's. The file seam is the one the upstream locator uses, because both
/// only check for and read a file.
/// </remarks>
/// <param name="fileAccess">How the locator checks for and reads a client's file.</param>
public sealed class CurlPortClientLocator(IUpstreamCurlFileAccess fileAccess)
{
    /// <summary>
    /// Finds the first client of <paramref name="platform"/> whose file exists at its expanded
    /// path and hashes to its pin, passing over any pin whose path an upstream curl pin lists.
    /// </summary>
    /// <param name="pins">The clients <c>CurlPortBuilds.json</c> pins.</param>
    /// <param name="upstreamPins">The builds <c>UpstreamCurlBuilds.json</c> pins, none of which is ever returned.</param>
    /// <param name="platform">The platform to find a client for, such as <c>win-x64</c>; see <see cref="UpstreamCurlLocator.CurrentPlatform"/>.</param>
    /// <returns>
    /// The verified client, or a location saying that no client is pinned for the platform, or
    /// that none of the pinned clients' files exists.
    /// </returns>
    /// <exception cref="UnpinnedCurlPortClientException">
    /// A pinned client's file exists but hashes to something other than its pin, or to an
    /// upstream curl pin, and no other pinned client of the platform verifies.
    /// </exception>
    /// <remarks>Only pins of <see cref="CurlPortClientKind.Curl"/> are candidates; the port's library is never run.</remarks>
    public CurlPortClientLocation Locate(
        IReadOnlyList<PinnedCurlPortClient> pins,
        IReadOnlyList<PinnedUpstreamCurlBuild> upstreamPins,
        string platform) =>
        LocateOfKind(pins, upstreamPins, platform, CurlPortClientKind.Curl);

    /// <summary>
    /// As <see cref="Locate"/>, for the port's managed <c>Libcurl</c> assembly: only pins of
    /// <see cref="CurlPortClientKind.Library"/> are candidates, so a curl is never handed to a libcurl
    /// driver (ADR-0089 decision 1 (c)).
    /// </summary>
    /// <param name="pins">The clients <c>CurlPortBuilds.json</c> pins.</param>
    /// <param name="upstreamPins">The builds <c>UpstreamCurlBuilds.json</c> pins, none of which is ever returned.</param>
    /// <param name="platform">The platform to find a library for, such as <c>win-x64</c>.</param>
    /// <returns>The verified library, or a location saying why none is available.</returns>
    /// <exception cref="UnpinnedCurlPortClientException">
    /// A pinned library's file exists but hashes to something other than its pin, or to an upstream
    /// curl pin, and no other pinned library of the platform verifies.
    /// </exception>
    public CurlPortClientLocation LocateLibrary(
        IReadOnlyList<PinnedCurlPortClient> pins,
        IReadOnlyList<PinnedUpstreamCurlBuild> upstreamPins,
        string platform) =>
        LocateOfKind(pins, upstreamPins, platform, CurlPortClientKind.Library);

    /// <summary>
    /// Checks that the file at <paramref name="path"/> is a pinned Curl port library, for a libcurl
    /// driver handed one with <c>--library</c>: its SHA-256 must be a library pin's and no upstream pin's.
    /// </summary>
    /// <param name="pins">The clients <c>CurlPortBuilds.json</c> pins.</param>
    /// <param name="upstreamPins">The builds <c>UpstreamCurlBuilds.json</c> pins.</param>
    /// <param name="path">The path of the assembly.</param>
    /// <returns>The first library pin whose SHA-256 the file matches.</returns>
    /// <exception cref="FileNotFoundException">No file exists at <paramref name="path"/>.</exception>
    /// <exception cref="UnpinnedCurlPortClientException">The file's SHA-256 matches no library pin, or an upstream pin.</exception>
    public PinnedCurlPortClient RequirePinnedLibrary(
        IReadOnlyList<PinnedCurlPortClient> pins,
        IReadOnlyList<PinnedUpstreamCurlBuild> upstreamPins,
        string path)
    {
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(upstreamPins);
        ArgumentNullException.ThrowIfNull(path);

        if (!fileAccess.FileExists(path))
        {
            throw new FileNotFoundException($"The Curl port library to call, {path}, was not found.", path);
        }

        var sha256 = HashFile(path);
        var isUpstream = upstreamPins.Any(pin => string.Equals(pin.Sha256, sha256, StringComparison.OrdinalIgnoreCase));
        return pins.FirstOrDefault(pin => !isUpstream && pin.Kind == CurlPortClientKind.Library && string.Equals(pin.Sha256, sha256, StringComparison.OrdinalIgnoreCase))
            ?? throw new UnpinnedCurlPortClientException(path, sha256);
    }

    private CurlPortClientLocation LocateOfKind(
        IReadOnlyList<PinnedCurlPortClient> pins,
        IReadOnlyList<PinnedUpstreamCurlBuild> upstreamPins,
        string platform,
        CurlPortClientKind kind)
    {
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(upstreamPins);
        ArgumentNullException.ThrowIfNull(platform);

        var upstreamPaths = upstreamPins.Select(pin => pin.DefaultPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var upstreamHashes = upstreamPins.Select(pin => pin.Sha256).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = pins
            .Where(pin => pin.Platform == platform && pin.Kind == kind)
            .Select(pin => (Client: pin, Path: Environment.ExpandEnvironmentVariables(pin.Path)))
            .Where(candidate => !upstreamPaths.Contains(candidate.Path))
            .ToList();

        return candidates.Count == 0
            ? CurlPortClientLocation.NoPinnedClient(platform)
            : VerifyFirstPresent(candidates, upstreamHashes);
    }

    /// <summary>
    /// Checks each candidate whose file exists, in order, and returns the first that hashes to
    /// its pin and to no upstream pin.
    /// </summary>
    private CurlPortClientLocation VerifyFirstPresent(List<(PinnedCurlPortClient Client, string Path)> candidates, HashSet<string> upstreamHashes)
    {
        UnpinnedCurlPortClientException? refusal = null;

        foreach (var (client, path) in candidates.Where(candidate => fileAccess.FileExists(candidate.Path)))
        {
            var sha256 = HashFile(path);

            if (string.Equals(sha256, client.Sha256, StringComparison.OrdinalIgnoreCase) && !upstreamHashes.Contains(sha256))
            {
                return CurlPortClientLocation.Found(client, path);
            }

            refusal ??= new UnpinnedCurlPortClientException(path, sha256);
        }

        return refusal is null ? CurlPortClientLocation.FileAbsent(candidates[0].Client, candidates[0].Path) : throw refusal;
    }

    private string HashFile(string path)
    {
        using var stream = fileAccess.OpenRead(path);

        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
