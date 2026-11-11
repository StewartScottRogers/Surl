namespace Surl.Conformance;

/// <summary>
/// What decides whether a test case can run against Surl with one pinned build: the build's
/// protocols and features, upstream's disabled list, what the server halves and the verify
/// checker accept, and decision 5's exclusions (ADR-0080 decision 3).
/// </summary>
/// <param name="DisabledCases">The case numbers <c>tests/data/DISABLED</c> lists.</param>
/// <param name="Protocols">The schemes the build's <c>protocols</c> line lists, lower case.</param>
/// <param name="Features">The build's features, as <see cref="UpstreamCurlBuildFeatures.For(CurlVersionReport)"/> derives them.</param>
/// <param name="SupportsServerCommand">
/// Whether the server half for a server name implements a <c>&lt;servercmd&gt;</c> command,
/// given the name and the command's first word; a case with a <c>&lt;postcmd&gt;</c> asks for
/// <see cref="UpstreamTestCaseServerCommands.PostCommand"/>.
/// </param>
/// <param name="SupportsStripLine">
/// Whether the verify checker interprets a strip line, given the part's name (<c>strip</c>,
/// <c>strippart</c>, <c>stripfile</c>, <c>stripfile1</c> ...) and the line without its newline.
/// </param>
/// <param name="TestServerChoices">The case numbers decision 5's exclusion list names.</param>
public sealed record UpstreamTestCaseSelectionRules(
    IReadOnlySet<int> DisabledCases,
    IReadOnlySet<string> Protocols,
    IReadOnlySet<string> Features,
    Func<string, string, bool> SupportsServerCommand,
    Func<string, string, bool> SupportsStripLine,
    IReadOnlySet<int> TestServerChoices)
{
    /// <summary>
    /// The rules for <paramref name="build"/> as Surl stands today: the server commands
    /// <see cref="UpstreamTestCaseServerCommands"/> lists for each built server half (BL-391 to
    /// BL-397 add their rows), the strip lines <see cref="UpstreamTestCaseStripLine.IsSupported"/>
    /// accepts, and the cases decision 5's exclusion list names (<see cref="UpstreamTestCaseExclusions.Parse"/>).
    /// </summary>
    /// <param name="build">The pinned build.</param>
    /// <param name="disabledCases">The case numbers <c>tests/data/DISABLED</c> lists.</param>
    /// <param name="testServerChoices">The case numbers <c>Upstream-Test-Case-Exclusions.md</c> lists.</param>
    /// <returns>The rules.</returns>
    public static UpstreamTestCaseSelectionRules ForBuild(PinnedUpstreamCurlBuild build, IReadOnlySet<int> disabledCases, IReadOnlySet<int> testServerChoices) =>
        ForClient(CurlVersionReport.FromPin(build), disabledCases, testServerChoices);

    /// <summary>
    /// The rules for the client <paramref name="report"/> describes, as <see cref="ForBuild"/> gives
    /// them for a pin: its own protocols and features decide what it can run, as runtests decides from
    /// the client's own <c>--version</c>. The Curl port's client is selected this way when it plays
    /// upstream's test cases (ADR-0089 decision 1(b)).
    /// </summary>
    /// <param name="report">What the client's <c>--version</c> says.</param>
    /// <param name="disabledCases">The case numbers <c>tests/data/DISABLED</c> lists.</param>
    /// <param name="testServerChoices">The case numbers <c>Upstream-Test-Case-Exclusions.md</c> lists.</param>
    /// <returns>The rules.</returns>
    public static UpstreamTestCaseSelectionRules ForClient(CurlVersionReport report, IReadOnlySet<int> disabledCases, IReadOnlySet<int> testServerChoices)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(disabledCases);
        ArgumentNullException.ThrowIfNull(testServerChoices);

        return new UpstreamTestCaseSelectionRules(
            disabledCases,
            report.Protocols.Select(protocol => protocol.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal),
            UpstreamCurlBuildFeatures.For(report),
            UpstreamTestCaseServerCommands.IsSupported,
            UpstreamTestCaseStripLine.IsSupported,
            testServerChoices);
    }
}
