namespace Surl.Conformance;

/// <summary>
/// A family's Phase 7 findings file, <c>Documentation/Conformance/CurlPort/&lt;Family&gt;.md</c>, and the
/// pass rule ADR-0089 decision 5 sets on it: a Phase 7 test fails on a Surl defect, on a port defect the
/// file does not list, and on a listed finding that no longer appears; it passes on agreement and on a
/// listed port defect. The file's one findings table has the Phase 7 case's name, in backticks, as its
/// first column; every other table row is ignored.
/// </summary>
internal static class CurlPortFindingsFile
{
    /// <summary>
    /// Applies decision 5 to <paramref name="measurement"/> of the case named <paramref name="caseName"/>,
    /// against the findings file <paramref name="fileName"/> under <c>Documentation/Conformance/CurlPort</c>.
    /// </summary>
    public static async Task AssertVerdictIsListedAsync(
        TestContext testContext, string fileName, string caseName, PinnedCurlPortMeasurement measurement)
    {
        var verdict = measurement.Verdict;
        Assert.AreNotEqual(CurlPortVerdictKind.SurlDefect, verdict.Kind, $"{caseName}: {verdict}");

        var path = Path.Combine(PinnedUpstreamCurl.RepositoryRoot(), "Documentation", "Conformance", "CurlPort", fileName);
        var listed = ListedCaseNames(await File.ReadAllLinesAsync(path, testContext.CancellationToken));
        var isListed = listed.Contains(caseName);
        switch (verdict.Kind)
        {
            case CurlPortVerdictKind.PortDefect:
                WriteBothRuns(testContext, verdict.Finding!);
                Assert.IsTrue(isListed, $"A port defect {fileName} does not list; add it (ADR-0089 decision 6):\n{verdict}");
                break;
            case CurlPortVerdictKind.Agreement:
                Assert.IsFalse(isListed, $"{fileName} lists {caseName}, but the port now agrees with upstream curl; remove the finding.");
                break;
            default:
                testContext.WriteLine($"{caseName}: {verdict}");
                break;
        }
    }

    // Writes what each client did, so the finding can be written up without running the case again.
    private static void WriteBothRuns(TestContext testContext, CurlPortFinding finding)
    {
        foreach (var (client, run) in new[] { ("upstream", finding.Upstream), ("port", finding.Port) })
        {
            testContext.WriteLine($"--- {client} exited {run.ExitCode}");
            testContext.WriteLine($"--- {client} stdout:\n{System.Text.Encoding.Latin1.GetString(run.StandardOutput)}");
            testContext.WriteLine($"--- {client} stderr:\n{run.StandardError}");
            foreach (var (bytes, index) in (run.Surl.ClientRequestBytes ?? []).Select((bytes, index) => (bytes, index)))
            {
                testContext.WriteLine(
                    $"--- {client} request bytes, connection {index}:\n" +
                    System.Text.Encoding.Latin1.GetString(CurlPortResultNormaliser.NormaliseRequestBytes(bytes, run.Surl.ClientFacingPort)));
            }

            testContext.WriteLine($"--- {client}'s surl notes:\n{run.Surl.Notes}");
        }
    }

    /// <summary>
    /// Gets the first cell of every table row whose first cell is a name in backticks.
    /// </summary>
    internal static IReadOnlySet<string> ListedCaseNames(IEnumerable<string> lines) =>
        lines
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("| `", StringComparison.Ordinal))
            .Select(line => line[3..line.IndexOf('`', 3)])
            .ToHashSet(StringComparer.Ordinal);
}
