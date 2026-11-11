using System.Globalization;

namespace Surl.Conformance;

/// <summary>
/// Reads the stated pass rate, <c>Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md</c>
/// (ADR-0080 decision 5): its <c>## Current</c> table, one row per platform, and its
/// <c>## Releases</c> table, one row per release and platform, in the order they were added.
/// </summary>
/// <param name="Current">The current table's rows.</param>
/// <param name="Releases">The releases table's rows, in file order.</param>
public sealed record UpstreamTestCasePassRateFile(
    IReadOnlyList<UpstreamTestCasePassRateRow> Current,
    IReadOnlyList<UpstreamTestCasePassRateRow> Releases)
{
    /// <summary>
    /// The file's path relative to the repository root.
    /// </summary>
    public const string Path = UpstreamTestCasePassingList.Folder + "/Upstream-Test-Case-Pass-Rate.md";

    /// <summary>
    /// The cell a platform not yet run holds instead of a number.
    /// </summary>
    public const string NotRun = "not run";

    /// <summary>
    /// Reads the file's two tables. Columns are found by their header: <c>Platform</c> and
    /// <c>Passed</c> in both, and <c>Version</c> in the releases table; a
    /// <c>test-server-choice</c> column is read when present. A number may hold thousands
    /// separators (<c>1,327</c>).
    /// </summary>
    /// <param name="markdown">The file's text.</param>
    /// <returns>The tables' rows.</returns>
    /// <exception cref="FormatException">
    /// A table or a column is missing, or a <c>Passed</c> cell is neither a number nor
    /// <see cref="NotRun"/>.
    /// </exception>
    public static UpstreamTestCasePassRateFile Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var lines = markdown.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        return new UpstreamTestCasePassRateFile(
            ReadTable(lines, "## Current", hasVersion: false),
            ReadTable(lines, "## Releases", hasVersion: true));
    }

    /// <summary>
    /// Each place a platform's passed count falls from one releases row to the next for that
    /// platform, which "only rises" forbids.
    /// </summary>
    /// <returns>One description per fall; empty when the count never falls.</returns>
    public IReadOnlyList<string> FindFallingReleases()
    {
        var falls = new List<string>();
        foreach (var platform in Releases.Where(row => row.Passed is not null).GroupBy(row => row.Platform, StringComparer.Ordinal))
        {
            var rows = platform.ToList();
            for (var index = 1; index < rows.Count; index++)
            {
                if (rows[index].Passed < rows[index - 1].Passed)
                {
                    falls.Add($"{platform.Key}: {rows[index].Version} passes {rows[index].Passed}, fewer than {rows[index - 1].Version}'s {rows[index - 1].Passed}");
                }
            }
        }

        return falls;
    }

    private static List<UpstreamTestCasePassRateRow> ReadTable(List<string> lines, string heading, bool hasVersion)
    {
        var table = ReadTableCells(lines, heading);
        var header = table[0];
        var platform = Column(header, "Platform", heading);
        var passed = Column(header, "Passed", heading);
        var version = hasVersion ? Column(header, "Version", heading) : -1;
        var testServerChoice = header.IndexOf(UpstreamTestCaseSkipReasons.TestServerChoice);

        if (table.Skip(2).FirstOrDefault(cells => cells.Count != header.Count) is { } ragged)
        {
            throw new FormatException($"A row of the pass-rate file's '{heading}' table has {ragged.Count} cells, not {header.Count}.");
        }

        return [.. table.Skip(2).Select(cells => new UpstreamTestCasePassRateRow(
            hasVersion ? cells[version] : null,
            cells[platform],
            ReadCount(cells[passed], "Passed"),
            testServerChoice >= 0 ? ReadCount(cells[testServerChoice], UpstreamTestCaseSkipReasons.TestServerChoice) : null))];
    }

    private static List<List<string>> ReadTableCells(List<string> lines, string heading)
    {
        var start = lines.FindIndex(line => line.Trim() == heading);
        if (start < 0)
        {
            throw new FormatException($"The pass-rate file has no '{heading}' heading.");
        }

        var table = lines.Skip(start + 1)
            .SkipWhile(line => !IsTableLine(line))
            .TakeWhile(IsTableLine)
            .Select(Cells)
            .ToList();
        return table.Count >= 2 ? table : throw new FormatException($"The pass-rate file's '{heading}' section has no table.");
    }

    private static bool IsTableLine(string line) => line.TrimStart().StartsWith('|');

    private static List<string> Cells(string line) =>
        [.. line.Trim().Trim('|').Split('|').Select(cell => cell.Trim().Trim('`'))];

    private static int Column(List<string> header, string name, string heading)
    {
        var index = header.IndexOf(name);
        return index >= 0 ? index : throw new FormatException($"The pass-rate file's '{heading}' table has no '{name}' column.");
    }

    private static int? ReadCount(string cell, string column)
    {
        if (cell == NotRun)
        {
            return null;
        }

        return int.TryParse(cell, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new FormatException($"The pass-rate file's {column} cell '{cell}' is neither a number nor '{NotRun}'.");
    }
}
