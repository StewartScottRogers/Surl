using System.Text;

namespace Surl.Conformance;

/// <summary>
/// One Phase 7 finding: a case on which the Curl port disagrees with pinned upstream curl, with
/// everything ADR-0089 decision 6 says a finding gives so the port's maintainer can reproduce it.
/// Upstream is right (ADR-0003 decision 6).
/// </summary>
/// <param name="Test">The Phase 7 test's method name.</param>
/// <param name="Mirrors">The test it mirrors.</param>
/// <param name="CommandLine">The command line, with the <c>surl</c> base URL left as its placeholder.</param>
/// <param name="UpstreamBuild">The paired upstream build: its SHA-256 and version line.</param>
/// <param name="PortClient">The Curl port client measured: its commit and SHA-256.</param>
/// <param name="SurlCommit">The Surl commit the case was measured at.</param>
/// <param name="Upstream">Upstream's run, unnormalised.</param>
/// <param name="Port">The port's run, unnormalised.</param>
/// <param name="Differences">Every compared item that differs, after normalisation.</param>
public sealed record CurlPortFinding(
    string Test,
    string Mirrors,
    string CommandLine,
    PinnedUpstreamCurlBuild UpstreamBuild,
    PinnedCurlPortClient PortClient,
    string SurlCommit,
    CurlPortMeasuredRun Upstream,
    CurlPortMeasuredRun Port,
    IReadOnlyList<CurlPortDifference> Differences)
{
    /// <inheritdoc/>
    public override string ToString()
    {
        var text = new StringBuilder()
            .AppendLine($"Phase 7 finding in {Test}, mirroring {Mirrors}")
            .AppendLine($"command line: {CommandLine}")
            .AppendLine($"upstream curl: {UpstreamBuild.Sha256} ({UpstreamBuild.Version})")
            .AppendLine($"Curl port: commit {PortClient.Commit} ({PortClient.Sha256})")
            .AppendLine($"Surl: commit {SurlCommit}");
        foreach (var difference in Differences)
        {
            text.AppendLine(difference.ToString());
        }

        return text.ToString();
    }
}
