namespace Surl.Conformance;

/// <summary>
/// The upstream test cases played with the supplementary Debug build of upstream curl instead of
/// the reference build (ADR-0173 decisions 6 and 7).
/// </summary>
public static class UpstreamTestCaseDebugBuildCases
{
    /// <summary>
    /// Gets the http/2 and http/3 cases whose <c>&lt;features&gt;</c> list <c>Debug</c>: 2400, 2401,
    /// 2406, 2501, 358 and 359. Every other case stays on its reference build.
    /// </summary>
    public static IReadOnlySet<int> Numbers { get; } = new HashSet<int> { 358, 359, 2400, 2401, 2406, 2501 };

    /// <summary>
    /// Tells whether <paramref name="number"/> is played with the Debug build on a platform that pins one.
    /// </summary>
    /// <param name="number">The case's number.</param>
    /// <param name="debugBuildPinned">Whether the platform pins a Debug build that is present and verified.</param>
    /// <returns><see langword="true"/> for one of <see cref="Numbers"/> when a Debug build is pinned.</returns>
    public static bool UsesDebugBuild(int number, bool debugBuildPinned) => debugBuildPinned && Numbers.Contains(number);
}
