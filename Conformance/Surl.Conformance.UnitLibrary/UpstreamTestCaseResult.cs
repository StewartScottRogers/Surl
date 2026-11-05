using System.Globalization;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// One line of a run's results file (ADR-0080 decision 5): <c>test&lt;N&gt; pass</c>,
/// <c>test&lt;N&gt; fail &lt;first differing section&gt;</c> (or <c>timeout</c>) or
/// <c>test&lt;N&gt; skip &lt;reason&gt;</c>.
/// </summary>
/// <param name="Number">The case's number.</param>
/// <param name="Outcome">How the run ended.</param>
/// <param name="Reason">The first differing section or the skip reason; <see langword="null"/> for a pass.</param>
public sealed record UpstreamTestCaseResult(int Number, UpstreamTestCaseOutcome Outcome, string? Reason)
{
    /// <summary>
    /// The reason a case fails when curl is stopped at the case's timeout.
    /// </summary>
    public const string TimeoutReason = "timeout";

    /// <summary>
    /// The result of a case that passed.
    /// </summary>
    /// <param name="number">The case's number.</param>
    /// <returns>The result.</returns>
    public static UpstreamTestCaseResult Pass(int number) => new(number, UpstreamTestCaseOutcome.Pass, null);

    /// <summary>
    /// The result of a case that failed.
    /// </summary>
    /// <param name="number">The case's number.</param>
    /// <param name="reason">The first differing section, or <see cref="TimeoutReason"/>.</param>
    /// <returns>The result.</returns>
    public static UpstreamTestCaseResult Fail(int number, string reason) => new(number, UpstreamTestCaseOutcome.Fail, reason);

    /// <summary>
    /// The result of a case that did not run.
    /// </summary>
    /// <param name="number">The case's number.</param>
    /// <param name="reason">The skip reason.</param>
    /// <returns>The result.</returns>
    public static UpstreamTestCaseResult Skip(int number, string reason) => new(number, UpstreamTestCaseOutcome.Skip, reason);

    /// <summary>
    /// The name of a run's results file for a pin's platform:
    /// <c>upstream-test-case-results.&lt;platform&gt;.txt</c>.
    /// </summary>
    /// <param name="platform">The pin's platform, such as <c>win-x64</c>.</param>
    /// <returns>The file name.</returns>
    public static string FileNameFor(string platform) => $"upstream-test-case-results.{platform}.txt";

    /// <summary>
    /// Writes the results file's text: one line per result in case-number order, each ending
    /// with a line feed.
    /// </summary>
    /// <param name="results">The results.</param>
    /// <returns>The text.</returns>
    public static string Format(IEnumerable<UpstreamTestCaseResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var text = new StringBuilder();
        foreach (var result in results.OrderBy(result => result.Number))
        {
            text.Append(result).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// The result's line, without its line feed.
    /// </summary>
    /// <returns>The line.</returns>
    public override string ToString()
    {
        var line = string.Create(CultureInfo.InvariantCulture, $"test{Number} {Outcome.ToString().ToLowerInvariant()}");

        return Reason is null ? line : $"{line} {Reason}";
    }
}
