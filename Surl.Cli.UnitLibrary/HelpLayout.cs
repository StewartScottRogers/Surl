namespace Surl.Cli;

/// <summary>
/// The line layouts of ADR-0034 decision 3, 79 columns wide: curl's measured column rule for
/// option lines, the pointer lines' wrapped name list, and the indented paragraphs of an
/// option page.
/// </summary>
internal static class HelpLayout
{
    /// <summary>The width of every help page: what curl uses with its stdout redirected.</summary>
    public const int Columns = 79;

    /// <summary>The indent of every paragraph line of an option page.</summary>
    public const string ParagraphIndent = "        ";

    private const int MinimumWidth = 5;

    /// <summary>
    /// Lays out one page's option lines by curl's column rule (ADR-0034, "The column rule"):
    /// the description column follows the longest left side of the page, pulled left only
    /// where a line would pass <see cref="Columns"/>.
    /// </summary>
    /// <param name="rows">Each line's left side (<c>-m, --max-time &lt;seconds&gt;</c>) and description.</param>
    /// <returns>The lines, without line ends.</returns>
    public static IReadOnlyList<string> FormatOptionLines(IReadOnlyList<(string LeftSide, string Description)> rows)
    {
        var longestLeftSide = Math.Max(MinimumWidth, rows.Max(row => row.LeftSide.Length));
        var longestDescription = Math.Max(MinimumWidth, rows.Max(row => row.Description.Length));
        if (longestLeftSide + longestDescription > Columns)
        {
            longestLeftSide = Columns - longestDescription;
        }

        return [.. rows.Select(row => FormatOptionLine(row.LeftSide, row.Description, longestLeftSide))];
    }

    /// <summary>
    /// Joins <paramref name="names"/> with <c>, </c>, ends them with <c>.</c>, and wraps them so
    /// each line holds as many names as fit in <see cref="Columns"/>, counting the <c>, </c>
    /// after each; no line keeps a trailing space.
    /// </summary>
    /// <param name="names">The names, in the order they are listed.</param>
    /// <returns>The lines, without line ends.</returns>
    public static IReadOnlyList<string> WrapNameList(IReadOnlyList<string> names)
    {
        var pieces = names.Select((name, index) => index == names.Count - 1 ? name + "." : name + ", ");
        return Wrap(pieces, string.Empty, countTrailingSpace: true);
    }

    /// <summary>
    /// Wraps <paramref name="paragraph"/> greedily at single spaces into lines of
    /// <see cref="ParagraphIndent"/> and words, none passing <see cref="Columns"/> unless it
    /// holds one word too long for any line.
    /// </summary>
    /// <param name="paragraph">The paragraph, its words separated by single spaces.</param>
    /// <returns>The lines, without line ends.</returns>
    public static IReadOnlyList<string> WrapParagraph(string paragraph) =>
        Wrap(paragraph.Split(' ').Select(word => word + " "), ParagraphIndent, countTrailingSpace: false);

    private static string FormatOptionLine(string leftSide, string description, int longestLeftSide)
    {
        var width = longestLeftSide;
        if (width + description.Length >= Columns - 2)
        {
            width = description.Length < Columns - 2 ? Columns - 3 - description.Length : 0;
        }

        return " " + leftSide.PadRight(Math.Max(0, width)) + "  " + description;
    }

    /// <summary>
    /// Puts each piece on the current line while the line stays within <see cref="Columns"/>,
    /// counting a piece's trailing space only when <paramref name="countTrailingSpace"/> is set,
    /// then trims each line's end.
    /// </summary>
    private static List<string> Wrap(IEnumerable<string> pieces, string indent, bool countTrailingSpace)
    {
        var lines = new List<string>();
        var line = indent;
        foreach (var piece in pieces)
        {
            if (line.Length > indent.Length && line.Length + (countTrailingSpace ? piece : piece.TrimEnd()).Length > Columns)
            {
                lines.Add(line.TrimEnd());
                line = indent;
            }

            line += piece;
        }

        lines.Add(line.TrimEnd());
        return lines;
    }
}
