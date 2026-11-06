using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// Upstream curl's test cases played by both clients (ADR-0089 decision 1(b)), judged against
/// <c>Documentation/Conformance/CurlPort/Upstream-Test-Cases.md</c> by decision 5's pass rule: the run
/// fails on a Surl defect (a case upstream no longer passes), on a port defect the file does not list,
/// and on a listed finding that no longer appears; it passes on agreement, on a listed port defect and
/// on a case not comparable.
/// </summary>
public sealed class UpstreamTestCasePortComparison
{
    private static readonly Regex FindingRow = new("^\\| `test([0-9]+)`", RegexOptions.CultureInvariant);

    private UpstreamTestCasePortComparison(IReadOnlyList<UpstreamTestCasePortRow> rows, IReadOnlySet<int> listedFindings)
    {
        Rows = rows;
        PortDefects = [.. rows.Where(row => row.Verdict == CurlPortVerdictKind.PortDefect)];
        SurlDefects = [.. rows.Where(row => row.Verdict == CurlPortVerdictKind.SurlDefect)];
        UnlistedPortDefects = [.. PortDefects.Where(row => !listedFindings.Contains(row.Number))];
        var defects = PortDefects.Select(row => row.Number).ToHashSet();
        FindingsNoLongerAppearing = [.. listedFindings.Where(number => !defects.Contains(number)).Order()];
        Families = CountFamilies(rows);
    }

    /// <summary>
    /// Gets every case played, in case number order.
    /// </summary>
    public IReadOnlyList<UpstreamTestCasePortRow> Rows { get; }

    /// <summary>
    /// Gets each family's counts, in ordinal order of the family's name.
    /// </summary>
    public IReadOnlyList<UpstreamTestCasePortFamilyCount> Families { get; }

    /// <summary>
    /// Gets the cases upstream passed and the port did not: the port's findings.
    /// </summary>
    public IReadOnlyList<UpstreamTestCasePortRow> PortDefects { get; }

    /// <summary>
    /// Gets the cases upstream did not pass. Each is Surl's own work, never a port finding.
    /// </summary>
    public IReadOnlyList<UpstreamTestCasePortRow> SurlDefects { get; }

    /// <summary>
    /// Gets the port defects the findings file does not list.
    /// </summary>
    public IReadOnlyList<UpstreamTestCasePortRow> UnlistedPortDefects { get; }

    /// <summary>
    /// Gets the case numbers the findings file lists that are no longer port defects.
    /// </summary>
    public IReadOnlyList<int> FindingsNoLongerAppearing { get; }

    /// <summary>
    /// Gets a value indicating whether decision 5's pass rule holds: no Surl defect, every port defect
    /// listed, and every listed finding still appearing.
    /// </summary>
    public bool Passes => SurlDefects.Count == 0 && UnlistedPortDefects.Count == 0 && FindingsNoLongerAppearing.Count == 0;

    /// <summary>
    /// Judges <paramref name="rows"/> against the findings file's listed case numbers.
    /// </summary>
    /// <param name="rows">Each case played by both clients.</param>
    /// <param name="listedFindings">The case numbers the findings file lists (<see cref="ListedFindings"/>).</param>
    /// <returns>The comparison.</returns>
    public static UpstreamTestCasePortComparison Compare(
        IEnumerable<UpstreamTestCasePortRow> rows, IReadOnlySet<int> listedFindings)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(listedFindings);

        return new UpstreamTestCasePortComparison([.. rows.OrderBy(row => row.Number)], listedFindings);
    }

    /// <summary>
    /// Reads the case numbers a findings file lists: the first cell of each table row that is
    /// <c>`test&lt;N&gt;`</c>. Every other line is ignored.
    /// </summary>
    /// <param name="lines">The findings file's lines.</param>
    /// <returns>The listed case numbers.</returns>
    public static IReadOnlySet<int> ListedFindings(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        return lines
            .Select(line => FindingRow.Match(line.Trim()))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToHashSet();
    }

    /// <summary>
    /// Describes what breaks decision 5's pass rule, one line per case, for an assertion's message;
    /// empty when the rule holds.
    /// </summary>
    /// <returns>The description.</returns>
    public string DescribeFailures()
    {
        var text = new StringBuilder();
        foreach (var row in SurlDefects)
        {
            text.Append("Surl defect, upstream curl did not pass a case its passing list names: ").Append(row).Append('\n');
        }

        foreach (var row in UnlistedPortDefects)
        {
            text.Append("A port defect Upstream-Test-Cases.md does not list; add it (ADR-0089 decision 6): ").Append(row).Append('\n');
        }

        foreach (var number in FindingsNoLongerAppearing)
        {
            text.Append(CultureInfo.InvariantCulture, $"Upstream-Test-Cases.md lists test{number}, but it is no longer a port defect; remove the finding.\n");
        }

        return text.ToString();
    }

    /// <summary>
    /// Writes the per-family counts as the findings file's table, a header row and one row per family
    /// and a total, so a run's log can be copied into the file.
    /// </summary>
    /// <returns>The table's Markdown lines.</returns>
    public string FormatFamilyTable()
    {
        var text = new StringBuilder();
        text.Append("| Family | Cases | Upstream passed | Port passed | Port defects | Not comparable |\n");
        text.Append("| --- | ---: | ---: | ---: | ---: | ---: |\n");
        foreach (var family in Families)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"| {family.Family} | {family.Cases} | {family.UpstreamPassed} | {family.PortPassed} | {family.PortDefects} | {family.NotComparable} |\n");
        }

        text.Append(FormatTotalRow());
        return text.ToString();
    }

    private static List<UpstreamTestCasePortFamilyCount> CountFamilies(IReadOnlyList<UpstreamTestCasePortRow> rows) =>
    [
        .. rows
            .GroupBy(row => row.Family, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(CountFamily),
    ];

    private static UpstreamTestCasePortFamilyCount CountFamily(IGrouping<string, UpstreamTestCasePortRow> family) => new(
        family.Key,
        family.Count(),
        family.Count(row => row.Upstream.Outcome == UpstreamTestCaseOutcome.Pass),
        family.Count(row => row.Port.Outcome == UpstreamTestCaseOutcome.Pass),
        family.Count(row => row.Verdict == CurlPortVerdictKind.PortDefect),
        family.Count(row => row.Verdict == CurlPortVerdictKind.NotComparable));

    private string FormatTotalRow() => string.Create(
        CultureInfo.InvariantCulture,
        $"| **All** | {Families.Sum(family => family.Cases)} | {Families.Sum(family => family.UpstreamPassed)} | {Families.Sum(family => family.PortPassed)} | {Families.Sum(family => family.PortDefects)} | {Families.Sum(family => family.NotComparable)} |\n");
}
