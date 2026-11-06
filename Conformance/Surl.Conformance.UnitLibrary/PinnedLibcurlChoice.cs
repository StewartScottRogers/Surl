namespace Surl.Conformance;

/// <summary>
/// Which libcurl a driver of libcurl's API may load, decided before anything is loaded: the file
/// named on its command line, or the one pinned for the platform, and only when its SHA-256
/// matches a pin of kind <see cref="UpstreamCurlBuildKind.Library"/> (ADR-0071 decision 10).
/// </summary>
/// <param name="LibraryPath">The verified library to load, or <see langword="null"/> when none may be.</param>
/// <param name="ExitCode">0 with a library; 3 when the file is missing or not a pinned libcurl; 4 (inconclusive) when no libcurl is pinned or installed for the platform, as on Linux and macOS.</param>
/// <param name="Message">One sentence saying why, for standard error when there is no library.</param>
public sealed record PinnedLibcurlChoice(string? LibraryPath, int ExitCode, string Message)
{
    /// <summary>The exit code for a requested file that is missing or not a pinned libcurl.</summary>
    public const int NotPinnedExitCode = 3;

    /// <summary>The exit code when no libcurl is pinned or installed for the platform.</summary>
    public const int InconclusiveExitCode = 4;

    /// <summary>
    /// Chooses the library: <paramref name="requestedPath"/> when given and pinned, else the libcurl
    /// pinned for <paramref name="platform"/> when its file is present and verifies.
    /// </summary>
    /// <param name="locator">The locator that hashes and checks the files.</param>
    /// <param name="pins">The builds <c>UpstreamCurlBuilds.json</c> pins.</param>
    /// <param name="requestedPath">The full path <c>--library</c> named, or <see langword="null"/>.</param>
    /// <param name="platform">The platform, such as <c>win-x64</c>.</param>
    /// <returns>The choice; its <see cref="LibraryPath"/> is set only for a verified pin.</returns>
    public static PinnedLibcurlChoice Choose(
        UpstreamCurlLocator locator,
        IReadOnlyList<PinnedUpstreamCurlBuild> pins,
        string? requestedPath,
        string platform)
    {
        ArgumentNullException.ThrowIfNull(locator);

        try
        {
            if (requestedPath is not null)
            {
                var pin = locator.RequirePinnedLibrary(pins, requestedPath);
                return new PinnedLibcurlChoice(requestedPath, 0, $"{requestedPath} matches the pinned libcurl for {pin.Platform}.");
            }

            var location = locator.LocateLibrary(pins, platform);
            return location.Build is null
                ? new PinnedLibcurlChoice(null, InconclusiveExitCode, $"Inconclusive: {location.Message}")
                : new PinnedLibcurlChoice(location.Build.DefaultPath, 0, location.Message);
        }
        catch (Exception exception) when (exception is UnpinnedUpstreamCurlException or FileNotFoundException)
        {
            return new PinnedLibcurlChoice(null, NotPinnedExitCode, exception.Message);
        }
    }
}
