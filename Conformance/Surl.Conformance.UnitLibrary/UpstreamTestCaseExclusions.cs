using System.Globalization;

namespace Surl.Conformance;

/// <summary>
/// Reads decision 5's exclusion list (ADR-0080),
/// <c>Documentation/Conformance/Upstream-Test-Case-Exclusions.md</c>: the upstream test cases whose
/// verified byte is the upstream test server's arbitrary choice, which the selection rules skip as
/// <see cref="UpstreamTestCaseSkipReasons.TestServerChoice"/>.
/// </summary>
public static class UpstreamTestCaseExclusions
{
    /// <summary>
    /// The file's path relative to the repository root.
    /// </summary>
    public const string Path = UpstreamTestCasePassingList.Folder + "/Upstream-Test-Case-Exclusions.md";

    /// <summary>
    /// The header of the column that names each row's case.
    /// </summary>
    public const string CaseColumn = "Case";

    /// <summary>
    /// Reads the case numbers from the file's table: the first Markdown table whose header has a
    /// <see cref="CaseColumn"/> column. A cell holds <c>test&lt;N&gt;</c> or <c>&lt;N&gt;</c>, with or
    /// without backticks. A table with no rows is an empty set.
    /// </summary>
    /// <param name="markdown">The file's text.</param>
    /// <returns>The excluded case numbers.</returns>
    /// <exception cref="FormatException">
    /// The file has no table with a <see cref="CaseColumn"/> column, a row's cell count differs
    /// from the header's, a cell names no case number, or a case is listed twice.
    /// </exception>
    public static IReadOnlySet<int> Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var table = ReadTable(markdown);
        var column = table[0].IndexOf(CaseColumn);
        var cases = new HashSet<int>();
        foreach (var row in table.Skip(2))
        {
            if (row.Count != table[0].Count)
            {
                throw new FormatException($"A row of the exclusions table has {row.Count} cells, not {table[0].Count}.");
            }

            var number = ReadCase(row[column]);
            if (!cases.Add(number))
            {
                throw new FormatException($"The exclusions table lists case {number} twice.");
            }
        }

        return cases;
    }

    private static List<List<string>> ReadTable(string markdown)
    {
        var lines = markdown.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        var header = lines.FindIndex(line => IsTableLine(line) && Cells(line).Contains(CaseColumn));
        if (header < 0)
        {
            throw new FormatException($"The exclusions file has no table with a '{CaseColumn}' column.");
        }

        return [.. lines.Skip(header).TakeWhile(IsTableLine).Select(Cells)];
    }

    private static int ReadCase(string cell)
    {
        var digits = cell.StartsWith("test", StringComparison.Ordinal) ? cell["test".Length..] : cell;
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new FormatException($"The exclusions table's {CaseColumn} cell '{cell}' is neither test<N> nor <N>.");
    }

    private static bool IsTableLine(string line) => line.TrimStart().StartsWith('|');

    private static List<string> Cells(string line) =>
        [.. line.Trim().Trim('|').Split('|').Select(cell => cell.Trim().Trim('`'))];
}
