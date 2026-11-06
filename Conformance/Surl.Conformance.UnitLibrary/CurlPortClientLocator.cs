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
    public CurlPortClientLocation Locate(
        IReadOnlyList<PinnedCurlPortClient> pins,
        IReadOnlyList<PinnedUpstreamCurlBuild> upstreamPins,
        string platform)
    {
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(upstreamPins);
        ArgumentNullException.ThrowIfNull(platform);

        var upstreamPaths = upstreamPins.Select(pin => pin.DefaultPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var upstreamHashes = upstreamPins.Select(pin => pin.Sha256).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = pins
            .Where(pin => pin.Platform == platform)
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
