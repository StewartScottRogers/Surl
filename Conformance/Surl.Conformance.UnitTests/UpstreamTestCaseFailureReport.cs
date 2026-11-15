using System.Collections.Concurrent;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Collects the full verdict of each failing upstream test case and writes the failures file
/// (<c>upstream-test-case-failures.&lt;platform&gt;.txt</c>) CI uploads beside the results file.
/// </summary>
internal sealed class UpstreamTestCaseFailureReport
{
    private readonly ConcurrentDictionary<int, string> details = new();

    /// <summary>The name of the failures file for a pin's platform.</summary>
    public static string FileNameFor(string platform) => $"upstream-test-case-failures.{platform}.txt";

    /// <summary>Records the detail of one failing case.</summary>
    public void Record(int number, string detail) => details[number] = detail;

    /// <summary>
    /// The block of one failing case: its results line, then the detail, then a blank line.
    /// </summary>
    public static string FormatBlock(UpstreamTestCaseResult result, string detail) =>
        $"{result}\n{detail.ReplaceLineEndings("\n").TrimEnd('\n')}\n\n";

    /// <summary>
    /// One block per <c>fail</c> result in case-number order; empty when nothing failed.
    /// </summary>
    public string Format(IEnumerable<UpstreamTestCaseResult> results)
    {
        var text = new StringBuilder();
        foreach (var result in results.Where(r => r.Outcome == UpstreamTestCaseOutcome.Fail).OrderBy(r => r.Number))
        {
            text.Append(FormatBlock(result, details.GetValueOrDefault(result.Number, "no detail recorded")));
        }

        return text.ToString();
    }
}
