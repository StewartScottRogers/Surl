namespace Surl.Conformance;

/// <summary>
/// One Phase 7 libcurl row (ADR-0089 decision 1 (c)): a step list of a pinned-libcurl test, run through
/// upstream's libcurl driver and the Curl port's twin of it, each against its own fresh <c>surl</c>.
/// </summary>
/// <param name="Name">The Phase 7 test's method name, the first column of <c>Libcurl.md</c>'s findings table.</param>
/// <param name="Mirrors">The test it mirrors, as <c>Class.Method</c>.</param>
/// <param name="Script">The upstream driver the mirrored test runs, such as <c>Run-LibcurlWebSocketScript.cs</c>; the port's twin takes the same arguments.</param>
/// <param name="Arguments">
/// The driver's arguments - its options, the URL, then the steps - with <see cref="SurlAuthorityPlaceholder"/>
/// standing for the host and port of each run's own <c>surl</c>.
/// </param>
/// <param name="CheckMirroredAssertions">
/// The mirrored test's assertions applied to upstream's run (its report and the files its <c>surl</c> stored): <see langword="null"/> when upstream's run
/// is what the mirrored test asserts, otherwise what differs, which is a Surl defect (ADR-0089 decision 5).
/// </param>
public sealed record CurlPortLibcurlCase(
    string Name,
    string Mirrors,
    string Script,
    IReadOnlyList<string> Arguments,
    Func<CurlPortLibcurlRun, string?> CheckMirroredAssertions)
{
    /// <summary>
    /// The text in <see cref="Arguments"/> that stands for a run's <c>surl</c> host and port, such as
    /// <c>ws://{surl}/echo</c> or <c>rtsp://tester:secret@{surl}/rec.bin</c>.
    /// </summary>
    public const string SurlAuthorityPlaceholder = "{surl}";

    /// <summary>
    /// Gets the files, by name under <c>surl</c>'s served folder, whose stored bytes are compared after
    /// both runs (uploads); none by default.
    /// </summary>
    public IReadOnlyList<string> ComparedStoredFiles { get; init; } = [];

    /// <summary>
    /// Gets the command line a finding names: the upstream driver and its arguments, the placeholder left in.
    /// </summary>
    public string CommandLine => $"{Script} {string.Join(' ', Arguments)}";

    /// <summary>
    /// Gets the arguments for one run, with <see cref="SurlAuthorityPlaceholder"/> replaced by <paramref name="authority"/>.
    /// </summary>
    /// <param name="authority">That run's <c>surl</c> host and port, such as <c>127.0.0.1:50123</c>.</param>
    /// <returns>The arguments to pass to either driver.</returns>
    public IReadOnlyList<string> ArgumentsFor(string authority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authority);
        return [.. Arguments.Select(argument => argument.Replace(SurlAuthorityPlaceholder, authority, StringComparison.Ordinal))];
    }
}
