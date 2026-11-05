namespace Surl.Conformance;

/// <summary>
/// What decides whether a test case can run against Surl with one pinned build: the build's
/// protocols and features, upstream's disabled list, what the server halves and the verify
/// checker accept, and decision 5's exclusions (ADR-0080 decision 3).
/// </summary>
/// <param name="DisabledCases">The case numbers <c>tests/data/DISABLED</c> lists.</param>
/// <param name="Protocols">The schemes the build's <c>protocols</c> line lists, lower case.</param>
/// <param name="Features">The build's features, as <see cref="UpstreamCurlBuildFeatures.For"/> derives them.</param>
/// <param name="SupportsServerCommand">
/// Whether the server half for a server name implements a <c>&lt;servercmd&gt;</c> command,
/// given the name and the command's first word.
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
    /// The rules for <paramref name="build"/> as Surl stands today: no server half takes a
    /// server command and the verify checker interprets no strip line yet (BL-390 to BL-397
    /// build them), and nothing is excluded. Replace those with <c>with</c> as they land.
    /// </summary>
    /// <param name="build">The pinned build.</param>
    /// <param name="disabledCases">The case numbers <c>tests/data/DISABLED</c> lists.</param>
    /// <returns>The rules.</returns>
    public static UpstreamTestCaseSelectionRules ForBuild(PinnedUpstreamCurlBuild build, IReadOnlySet<int> disabledCases)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(disabledCases);

        return new UpstreamTestCaseSelectionRules(
            disabledCases,
            build.Protocols.Select(protocol => protocol.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal),
            UpstreamCurlBuildFeatures.For(build),
            static (_, _) => false,
            static (_, _) => false,
            new HashSet<int>());
    }
}
