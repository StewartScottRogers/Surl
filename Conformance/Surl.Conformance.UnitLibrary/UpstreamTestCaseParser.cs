using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// Reads a preprocessed upstream curl test case into an <see cref="UpstreamTestCase"/>, line by
/// line as FILEFORMAT.md lays it out: every tag on its own line, a <c>&lt;testcase&gt;</c>
/// holding sections, each section holding parts.
/// </summary>
/// <remarks>
/// The file is not read as an XML document: FILEFORMAT.md says "lack of support for character
/// entities is a big difference", and a part's lines are bytes, never markup. Like upstream's
/// <c>getpart</c> (<c>tests/getpart.pm</c> at tag <c>curl-8_21_0</c>) a part ends at the first
/// line starting with its own closing tag, counting nested openings of the same name; a tag is
/// recognised by its start, so text after it on the line is ignored (test 324's
/// <c>&lt;command&gt; # no --insecure</c>, test 2070's <c>&lt;/info&gt;type</c>); and its
/// attributes are read with the same pattern. Text inside a section but outside its parts is
/// kept as the section's <see cref="UpstreamTestCaseSection.Lines"/>. A self-closing
/// <c>&lt;name/&gt;</c> is read as an empty part; <c>getpart</c> has no such form. Where <c>getpart</c>
/// silently reads nothing - a part outside every section (FILEFORMAT's <c>info</c>,
/// <c>reply</c>, <c>client</c>, <c>disable</c> and <c>verify</c>), a section or part left open,
/// a closing tag that closes nothing, text outside <c>&lt;testcase&gt;</c> - this parser throws
/// an <see cref="UpstreamTestCaseFormatException"/> naming the source line, so a case never runs
/// with part of it silently lost.
/// </remarks>
public static class UpstreamTestCaseParser
{
    private static readonly HashSet<string> MainSections = new(StringComparer.Ordinal)
    {
        "info", "reply", "client", "disable", "verify",
    };

    /// <summary>
    /// Preprocesses and reads <paramref name="source"/>, the bytes of a test case file.
    /// </summary>
    /// <param name="source">The file's bytes.</param>
    /// <param name="environment">The variables, features, files and clock it depends on.</param>
    /// <returns>The test case.</returns>
    /// <exception cref="UpstreamTestCaseFormatException">The file cannot be preprocessed or read.</exception>
    public static UpstreamTestCase Parse(byte[] source, UpstreamTestCaseEnvironment environment) =>
        Parse(UpstreamTestCasePreprocessor.Preprocess(source, environment));

    /// <summary>
    /// Reads preprocessed lines into a test case.
    /// </summary>
    /// <param name="lines">The preprocessed lines.</param>
    /// <returns>The test case.</returns>
    /// <exception cref="UpstreamTestCaseFormatException">The lines do not form a test case.</exception>
    public static UpstreamTestCase Parse(IReadOnlyList<UpstreamTestCaseLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var reader = new LineReader(lines);
        var attributes = ReadUntilTestcase(reader);
        var sections = new List<UpstreamTestCaseSection>();

        for (var line = reader.NextStructuralLine(); !IsClosing(line, "testcase"); line = reader.NextStructuralLine())
        {
            sections.Add(ReadSection(reader, line));
        }

        RequireNothingAfter(reader);
        return new UpstreamTestCase(attributes, sections);
    }

    /// <summary>
    /// Reads the attributes of a tag line as upstream's <c>getpartattr</c> does.
    /// </summary>
    /// <param name="attributeText">The text after the tag name.</param>
    /// <returns>The attributes by name; a repeated name keeps its last value.</returns>
    public static IReadOnlyDictionary<string, string> ParseAttributes(string attributeText)
    {
        ArgumentNullException.ThrowIfNull(attributeText);

        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match attribute in Attribute.Matches(attributeText))
        {
            var value = attribute.Groups[3].Success ? attribute.Groups[3].Value : attribute.Groups[4].Value;
            attributes[attribute.Groups[1].Value] = value;
        }

        return attributes;
    }

    private static IReadOnlyDictionary<string, string> ReadUntilTestcase(LineReader reader)
    {
        var line = reader.NextStructuralLine();
        if (line.Text.StartsWith("<?xml", StringComparison.Ordinal))
        {
            line = reader.NextStructuralLine();
        }

        var tag = RequireOpening(line);
        if (tag.Name != "testcase")
        {
            throw Unexpected(line, "a <testcase> tag");
        }

        return tag.Attributes;
    }

    private static UpstreamTestCaseSection ReadSection(LineReader reader, UpstreamTestCaseLine opening)
    {
        var tag = RequireOpening(opening);
        if (!MainSections.Contains(tag.Name))
        {
            throw new UpstreamTestCaseFormatException(
                opening.SourceLineNumber,
                $"<{tag.Name}> is outside every section (info, reply, client, disable, verify), where upstream's getpart never reads it");
        }

        var parts = new List<UpstreamTestCasePart>();
        var lines = new List<string>();

        for (var line = reader.NextStructuralLine(); !IsClosing(line, tag.Name); line = reader.NextStructuralLine())
        {
            if (OpeningTag.IsMatch(line.Text))
            {
                parts.Add(ReadPart(reader, line, tag.Name));
            }
            else
            {
                lines.Add(line.Text);
            }
        }

        return new UpstreamTestCaseSection(tag.Name, tag.Attributes, parts, lines);
    }

    private static UpstreamTestCasePart ReadPart(LineReader reader, UpstreamTestCaseLine opening, string sectionName)
    {
        var tag = RequireOpening(opening);
        var lines = new List<string>();
        if (tag.SelfClosing)
        {
            return new UpstreamTestCasePart(tag.Name, tag.Attributes, lines);
        }

        var depth = 1;
        for (var line = reader.Next(tag.Name); ; line = reader.Next(tag.Name))
        {
            depth += Depth(line.Text, tag.Name);
            if (depth == 0)
            {
                return new UpstreamTestCasePart(tag.Name, tag.Attributes, lines);
            }

            RequireNotClosing(line, sectionName, tag.Name);
            lines.Add(line.Text);
        }
    }

    private static int Depth(string text, string name)
    {
        if (StartsWithTag(text, "</" + name))
        {
            return -1;
        }

        return StartsWithTag(text, "<" + name) ? 1 : 0;
    }

    private static bool StartsWithTag(string text, string tag)
    {
        var trimmed = text.TrimStart(' ');
        return trimmed.StartsWith(tag, StringComparison.Ordinal)
            && trimmed.Length > tag.Length
            && trimmed[tag.Length] is ' ' or '>';
    }

    private static void RequireNotClosing(UpstreamTestCaseLine line, string sectionName, string partName)
    {
        if (StartsWithTag(line.Text, "</" + sectionName))
        {
            throw new UpstreamTestCaseFormatException(
                line.SourceLineNumber, $"missing </{partName}> tag before </{sectionName}>");
        }
    }

    private static void RequireNothingAfter(LineReader reader)
    {
        var line = reader.NextStructuralLineOrNull();
        if (line is not null)
        {
            throw Unexpected(line, "nothing after </testcase>");
        }
    }

    private static Tag RequireOpening(UpstreamTestCaseLine line)
    {
        var match = OpeningTag.Match(line.Text);
        if (!match.Success)
        {
            throw Unexpected(line, "an opening tag");
        }

        return new Tag(match.Groups[1].Value, ParseAttributes(match.Groups[2].Value), match.Groups[3].Length > 0);
    }

    private static bool IsClosing(UpstreamTestCaseLine line, string name)
    {
        var match = ClosingTag.Match(line.Text);
        if (!match.Success)
        {
            return false;
        }

        if (match.Groups[1].Value != name)
        {
            throw Unexpected(line, $"</{name}>");
        }

        return true;
    }

    private static UpstreamTestCaseFormatException Unexpected(UpstreamTestCaseLine line, string expected) =>
        new(line.SourceLineNumber, $"expected {expected}, found '{line.Text.TrimEnd('\n')}'");

    private static readonly Regex Attribute = new(" *([^=]*)= *(\"([^\"]*)\"|'([^']*)')");

    private static readonly Regex OpeningTag = new(@"^ *<([A-Za-z][A-Za-z0-9_-]*)((?: [^>]*?)?)(/?)>");

    private static readonly Regex ClosingTag = new(@"^ *</([A-Za-z][A-Za-z0-9_-]*)>");

    private sealed record Tag(string Name, IReadOnlyDictionary<string, string> Attributes, bool SelfClosing);

    private sealed class LineReader(IReadOnlyList<UpstreamTestCaseLine> lines)
    {
        private int next;

        public UpstreamTestCaseLine Next(string openName)
        {
            if (next == lines.Count)
            {
                throw new UpstreamTestCaseFormatException(LastLineNumber(), $"<{openName}> is not closed");
            }

            return lines[next++];
        }

        public UpstreamTestCaseLine NextStructuralLine() =>
            NextStructuralLineOrNull()
            ?? throw new UpstreamTestCaseFormatException(LastLineNumber(), "the file ends before </testcase>");

        public UpstreamTestCaseLine? NextStructuralLineOrNull()
        {
            while (next < lines.Count)
            {
                var line = lines[next++];
                if (!IsIgnorable(line))
                {
                    return line;
                }
            }

            return null;
        }

        private bool IsIgnorable(UpstreamTestCaseLine line)
        {
            var trimmed = line.Text.Trim();
            if (trimmed.StartsWith("<!--", StringComparison.Ordinal))
            {
                SkipToCommentEnd(line);
                return true;
            }

            return trimmed.Length == 0 || trimmed.StartsWith('#');
        }

        private void SkipToCommentEnd(UpstreamTestCaseLine line)
        {
            for (var current = line; !current.Text.Contains("-->", StringComparison.Ordinal); current = Next("!--"))
            {
            }
        }

        private int LastLineNumber() => lines.Count == 0 ? 1 : lines[^1].SourceLineNumber;
    }
}
