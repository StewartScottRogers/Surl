namespace Surl.Conformance;

/// <summary>
/// One upstream test case played twice against Surl's server halves - by the paired pinned upstream
/// build and by the pinned Curl port - and ADR-0089 decision 5's verdict on the pair.
/// </summary>
/// <param name="Number">The case's number.</param>
/// <param name="Family">The protocol family of the case's first server (<c>http</c>, <c>ftp</c>, <c>mail</c> ...).</param>
/// <param name="Upstream">How the case ended with upstream curl, the oracle.</param>
/// <param name="Port">How the case ended with the Curl port.</param>
public sealed record UpstreamTestCasePortRow(
    int Number, string Family, UpstreamTestCaseResult Upstream, UpstreamTestCaseResult Port)
{
    /// <summary>
    /// Gets decision 5's verdict: a Surl defect when upstream did not pass (the port is not judged),
    /// not comparable when runtests would skip the case for the port (its own <c>--version</c> lacks
    /// what the case needs; the port's skip reason says what), agreement when the port passed too,
    /// and otherwise a port defect.
    /// </summary>
    public CurlPortVerdictKind Verdict => (Upstream.Outcome, Port.Outcome) switch
    {
        (not UpstreamTestCaseOutcome.Pass, _) => CurlPortVerdictKind.SurlDefect,
        (_, UpstreamTestCaseOutcome.Skip) => CurlPortVerdictKind.NotComparable,
        (_, UpstreamTestCaseOutcome.Pass) => CurlPortVerdictKind.Agreement,
        _ => CurlPortVerdictKind.PortDefect,
    };

    /// <summary>
    /// Gets the name the findings file gives the case, <c>test&lt;N&gt;</c>.
    /// </summary>
    public string Name => $"test{Number}";

    /// <summary>
    /// Describes the pair in one line: the case, its family, how each client ended and the verdict.
    /// </summary>
    /// <returns>The description.</returns>
    public override string ToString() =>
        $"{Name} ({Family}): upstream {Ending(Upstream)}, port {Ending(Port)}: {Verdict}";

    private static string Ending(UpstreamTestCaseResult result) =>
        result.Reason is null ? result.Outcome.ToString().ToLowerInvariant() : $"{result.Outcome.ToString().ToLowerInvariant()} {result.Reason}";
}
