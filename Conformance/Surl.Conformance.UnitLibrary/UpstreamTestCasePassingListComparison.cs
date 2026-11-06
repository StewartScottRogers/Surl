using System.Globalization;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// A run's results set against its platform's passing list: the guard that keeps the pass rate
/// from falling (ADR-0080 decision 5).
/// </summary>
/// <param name="Platform">The pin's platform, such as <c>win-x64</c>.</param>
/// <param name="Regressions">
/// Each listed case that did not pass, with how it ended; a listed case the run has no result for
/// is a failure with the reason <see cref="MissingReason"/>.
/// </param>
/// <param name="NewlyPassing">The numbers of the cases that passed but are not listed, ascending.</param>
public sealed record UpstreamTestCasePassingListComparison(
    string Platform,
    IReadOnlyList<UpstreamTestCaseResult> Regressions,
    IReadOnlyList<int> NewlyPassing)
{
    /// <summary>
    /// The reason given for a listed case the run wrote no result for.
    /// </summary>
    public const string MissingReason = "no-result";

    /// <summary>
    /// Whether every listed case passed.
    /// </summary>
    public bool HasRegressions => Regressions.Count > 0;

    /// <summary>
    /// Sets a run's results against a passing list.
    /// </summary>
    /// <param name="platform">The pin's platform.</param>
    /// <param name="passingList">The numbers the platform's passing list holds.</param>
    /// <param name="results">The run's results.</param>
    /// <returns>The comparison.</returns>
    public static UpstreamTestCasePassingListComparison Compare(
        string platform, IEnumerable<int> passingList, IEnumerable<UpstreamTestCaseResult> results)
    {
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(passingList);
        ArgumentNullException.ThrowIfNull(results);

        var listed = passingList.ToHashSet();
        var byNumber = results.ToDictionary(result => result.Number);

        var regressions = listed.Order()
            .Select(number => byNumber.GetValueOrDefault(number) ?? UpstreamTestCaseResult.Fail(number, MissingReason))
            .Where(result => result.Outcome != UpstreamTestCaseOutcome.Pass)
            .ToList();
        var newlyPassing = byNumber.Values
            .Where(result => result.Outcome == UpstreamTestCaseOutcome.Pass && !listed.Contains(result.Number))
            .Select(result => result.Number)
            .Order()
            .ToList();

        return new UpstreamTestCasePassingListComparison(platform, regressions, newlyPassing);
    }

    /// <summary>
    /// The message the guard fails with: each listed case that did not pass and how it ended.
    /// </summary>
    /// <returns>The message.</returns>
    public string DescribeRegressions()
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"{Regressions.Count} case(s) listed in {ListPath} did not pass; a passing case must keep passing:");
        foreach (var regression in Regressions)
        {
            text.Append('\n').Append("  ").Append(regression);
        }

        return text.ToString();
    }

    /// <summary>
    /// What the reader does when more cases pass than the list names: the lines to add to the
    /// passing list and the counts to raise. Empty when no case is newly passing.
    /// </summary>
    /// <returns>The message, or an empty string.</returns>
    public string DescribeNewlyPassing()
    {
        if (NewlyPassing.Count == 0)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture,
            $"{NewlyPassing.Count} case(s) pass that {ListPath} does not list. Add these lines to it, in order, and raise the {Platform} row's counts in {UpstreamTestCasePassRateFile.Path}:");
        foreach (var number in NewlyPassing)
        {
            text.Append('\n').Append(number.ToString(CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    private string ListPath => $"{UpstreamTestCasePassingList.Folder}/{UpstreamTestCasePassingList.FileNameFor(Platform)}";
}
