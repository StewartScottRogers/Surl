namespace Surl.Conformance;

/// <summary>
/// Which Curl port <c>Libcurl</c> assembly a port-side libcurl driver may call, decided before any of its
/// types is loaded: the file named on its command line, or the one pinned for the platform, and only when
/// its SHA-256 matches a <c>CurlPortBuilds.json</c> pin of kind <see cref="CurlPortClientKind.Library"/>
/// and no upstream pin (ADR-0089 decision 1 (c)). The twin of <see cref="PinnedLibcurlChoice"/>, with the
/// same exit codes, kept apart from it as every port type is kept apart from upstream's (ADR-0083 decision 2).
/// </summary>
/// <param name="LibraryPath">The verified assembly to call, or <see langword="null"/> when none may be.</param>
/// <param name="Pin">The pin the assembly matched, or <see langword="null"/> when there is none.</param>
/// <param name="ExitCode">0 with an assembly; 3 when the file is missing or not pinned; 4 (inconclusive) when none is pinned or installed for the platform.</param>
/// <param name="Message">One sentence saying why, for standard error when there is no assembly.</param>
public sealed record CurlPortLibcurlChoice(string? LibraryPath, PinnedCurlPortClient? Pin, int ExitCode, string Message)
{
    /// <summary>
    /// Chooses the assembly: <paramref name="requestedPath"/> when given and pinned, else the library
    /// pinned for <paramref name="platform"/> when its file is present and verifies.
    /// </summary>
    /// <param name="locator">The locator that hashes and checks the files.</param>
    /// <param name="pins">The clients <c>CurlPortBuilds.json</c> pins.</param>
    /// <param name="upstreamPins">The builds <c>UpstreamCurlBuilds.json</c> pins, none of which is ever chosen.</param>
    /// <param name="requestedPath">The full path <c>--library</c> named, or <see langword="null"/>.</param>
    /// <param name="platform">The platform, such as <c>win-x64</c>.</param>
    /// <returns>The choice; its <see cref="LibraryPath"/> is set only for a verified pin.</returns>
    public static CurlPortLibcurlChoice Choose(
        CurlPortClientLocator locator,
        IReadOnlyList<PinnedCurlPortClient> pins,
        IReadOnlyList<PinnedUpstreamCurlBuild> upstreamPins,
        string? requestedPath,
        string platform)
    {
        ArgumentNullException.ThrowIfNull(locator);

        try
        {
            if (requestedPath is not null)
            {
                var pin = locator.RequirePinnedLibrary(pins, upstreamPins, requestedPath);
                return new CurlPortLibcurlChoice(requestedPath, pin, 0, $"{requestedPath} matches the pinned Curl port library for {pin.Platform}, commit {pin.Commit}.");
            }

            var location = locator.LocateLibrary(pins, upstreamPins, platform);
            return location.Client is null
                ? new CurlPortLibcurlChoice(null, null, PinnedLibcurlChoice.InconclusiveExitCode, $"Inconclusive: {location.Message}")
                : new CurlPortLibcurlChoice(location.Path, location.Client, 0, location.Message);
        }
        catch (Exception exception) when (exception is UnpinnedCurlPortClientException or FileNotFoundException)
        {
            return new CurlPortLibcurlChoice(null, null, PinnedLibcurlChoice.NotPinnedExitCode, exception.Message);
        }
    }
}
