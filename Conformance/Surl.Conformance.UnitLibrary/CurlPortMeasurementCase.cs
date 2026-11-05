namespace Surl.Conformance;

/// <summary>
/// One Phase 7 case (ADR-0089): a command line, the <c>surl</c> it runs against and the upstream
/// build it is paired with. The same case runs once through pinned upstream curl and once through
/// the pinned Curl port, each against its own fresh <c>surl</c>.
/// </summary>
/// <param name="Name">The Phase 7 test's method name, the first column of its family's findings file.</param>
/// <param name="Mirrors">The test the case mirrors, as <c>Class.Method</c> (ADR-0089 decision 1).</param>
/// <param name="UpstreamProtocol">
/// The protocol <see cref="UpstreamCurlLocator.LocateForProtocol"/> picks the paired upstream build for
/// (ADR-0089 decision 2).
/// </param>
/// <param name="Arguments">
/// curl's arguments, each <see cref="SurlBaseUrlPlaceholder"/> standing for the base URL of the
/// <c>surl</c> that run talks to.
/// </param>
/// <param name="SurlScheme">The scheme <c>surl</c> listens on, e.g. <c>http</c>.</param>
/// <param name="SurlOptions"><c>surl</c>'s options, given identically to both runs.</param>
/// <param name="Files">The content <c>surl</c> serves, by file name, copied afresh for each run.</param>
/// <param name="CheckMirroredAssertions">
/// The mirrored test's assertions applied to upstream's run: <see langword="null"/> when upstream's run
/// is what the mirrored test asserts, otherwise what differs. A non-null answer is a Surl defect
/// (ADR-0089 decision 5).
/// </param>
public sealed record CurlPortMeasurementCase(
    string Name,
    string Mirrors,
    string UpstreamProtocol,
    IReadOnlyList<string> Arguments,
    string SurlScheme,
    IReadOnlyList<string> SurlOptions,
    IReadOnlyDictionary<string, byte[]> Files,
    Func<CurlPortMeasuredRun, string?> CheckMirroredAssertions)
{
    /// <summary>
    /// The text in <see cref="Arguments"/> that stands for the base URL of a run's <c>surl</c>,
    /// trailing slash included, e.g. <c>{surl}/hello.txt</c>.
    /// </summary>
    public const string SurlBaseUrlPlaceholder = "{surl}/";

    /// <summary>
    /// Gets the fragments of the standard error lines the mirrored test asserts on. Only lines
    /// containing one of them are compared (ADR-0089 decision 4); empty compares no standard error.
    /// </summary>
    public IReadOnlyList<string> AssertedStandardErrorFragments { get; init; } = [];

    /// <summary>
    /// Gets the names of the files <c>surl</c> stored that the mirrored test asserts on (uploads),
    /// compared byte for byte.
    /// </summary>
    public IReadOnlyList<string> ComparedStoredFiles { get; init; } = [];

    /// <summary>
    /// Gets the upstream release-notes entry, between 8.21.0 and the paired build's version, that
    /// describes a difference this case shows, or <see langword="null"/> when none does. When it is
    /// set and the paired build is not 8.21.0, a disagreement is not comparable: release difference
    /// (ADR-0089 decision 2).
    /// </summary>
    public string? ReleaseDifference { get; init; }

    /// <summary>
    /// Gets the command line as the findings file writes it, with the placeholder left in.
    /// </summary>
    public string CommandLine => "curl " + string.Join(' ', Arguments);

    /// <summary>
    /// Gets curl's arguments for a run against the <c>surl</c> at <paramref name="surlBaseUrl"/>.
    /// </summary>
    /// <param name="surlBaseUrl">The base URL that run's <c>surl</c> printed.</param>
    /// <returns>The arguments with every placeholder replaced.</returns>
    public IReadOnlyList<string> ArgumentsFor(Uri surlBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(surlBaseUrl);
        return [.. Arguments.Select(argument => argument.Replace(SurlBaseUrlPlaceholder, surlBaseUrl.AbsoluteUri, StringComparison.Ordinal))];
    }
}
