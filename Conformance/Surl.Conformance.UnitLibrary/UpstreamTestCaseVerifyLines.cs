using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// The line operations upstream's runtests (<c>tests/runtests.pl</c>, <c>tests/getpart.pm</c> at
/// tag <c>curl-8_21_0</c>) performs on both sides of a verify comparison, one character per byte.
/// </summary>
internal static class UpstreamTestCaseVerifyLines
{
    private static readonly IReadOnlyDictionary<string, string> NoAttributes = new Dictionary<string, string>(StringComparer.Ordinal);

    private static readonly Regex Alternatives = new(@"%alternatives\[([^,]*),([^\]]*)\]", RegexOptions.CultureInvariant);

    /// <summary>
    /// <c>loadarray</c>: bytes split after each line feed.
    /// </summary>
    public static List<string> Split(byte[] bytes) => Split(Encoding.Latin1.GetString(bytes));

    /// <summary>
    /// <c>getpart</c>: the lines of every part of that name in the section, joined.
    /// </summary>
    public static List<string> Of(UpstreamTestCaseSection? section, string part) =>
        (section?.Parts ?? []).Where(candidate => candidate.Name == part).SelectMany(candidate => candidate.Lines).ToList();

    /// <summary>
    /// <c>partexists</c>.
    /// </summary>
    public static bool Exists(UpstreamTestCaseSection? section, string part) => section?.FindPart(part) is not null;

    /// <summary>
    /// <c>getpartattr</c>: the first part's attributes, or none.
    /// </summary>
    public static IReadOnlyDictionary<string, string> AttributesOf(UpstreamTestCaseSection? section, string part) =>
        section?.FindPart(part)?.Attributes ?? NoAttributes;

    /// <summary>
    /// Perl's truthiness of a hash value: present, not empty and not <c>0</c>.
    /// </summary>
    public static bool IsSet(IReadOnlyDictionary<string, string> attributes, string name) =>
        attributes.TryGetValue(name, out var value) && IsTrue(value);

    /// <summary>
    /// Perl's truthiness of a string: defined, not empty and not <c>0</c>.
    /// </summary>
    public static bool IsTrue(string? value) => value is { Length: > 0 } and not "0";

    /// <summary>
    /// Whether the attribute is exactly <c>text</c>.
    /// </summary>
    public static bool IsTextMode(IReadOnlyDictionary<string, string> attributes) =>
        attributes.TryGetValue("mode", out var mode) && mode == "text";

    /// <summary>
    /// <c>normalize_text</c>: every newline made CRLF.
    /// </summary>
    public static void NormalizeText(List<string> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            lines[index] = lines[index].Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// <c>chomp($lines[-1])</c>.
    /// </summary>
    public static void CutFinalNewline(List<string> lines)
    {
        if (lines.Count > 0 && lines[^1].EndsWith('\n'))
        {
            lines[^1] = lines[^1][..^1];
        }
    }

    /// <summary>
    /// <c>subnewlines</c> as a <c>crlf</c> attribute asks: <c>headers</c> guesses the header lines,
    /// any other set value rewrites every line.
    /// </summary>
    public static void RewriteNewlines(List<string> lines, IReadOnlyDictionary<string, string> attributes)
    {
        if (!IsSet(attributes, "crlf"))
        {
            return;
        }

        Func<string, string> rewrite = attributes["crlf"] == "headers"
            ? new UpstreamTestCaseNewlines().GuessHeaderCrlf
            : UpstreamTestCaseNewlines.ForceCrlf;

        for (var index = 0; index < lines.Count; index++)
        {
            lines[index] = rewrite(lines[index]);
        }
    }

    /// <summary>
    /// The strip lines of a part, each read with <see cref="UpstreamTestCaseStripLine.TryParse"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">A line is outside ADR-0080 decision 4's subset; the selector skips such a case first.</exception>
    public static List<UpstreamTestCaseStripLine> StripLinesOf(UpstreamTestCaseSection? section, string part) =>
        Of(section, part).Select(line => ReadStripLine(Chomp(line))).ToList();

    /// <summary>
    /// The <c>&lt;strip&gt;</c> regular expressions; a blank line is skipped.
    /// </summary>
    /// <exception cref="NotSupportedException">A pattern is outside the subset.</exception>
    public static List<Regex> StripPatternsOf(UpstreamTestCaseSection? section) =>
        Of(section, "strip")
            .Select(Chomp)
            .Where(line => line.Length > 0)
            .Select(line => UpstreamTestCasePerlRegex.TryCreate(line, ignoreCase: false)
                ?? throw new NotSupportedException($"The strip pattern '{line}' is outside ADR-0080 decision 4's subset."))
            .ToList();

    /// <summary>
    /// The <c>&lt;stdout&gt;</c>, <c>&lt;stderr&gt;</c> and <c>&lt;file&gt;</c> strip loop: each
    /// operation over every line in turn, keeping only lines Perl counts as true afterwards.
    /// </summary>
    public static List<string> StripDroppingFalseLines(List<string> lines, IEnumerable<UpstreamTestCaseStripLine> strips) =>
        strips.Aggregate(lines, (current, strip) => current.Select(strip.Apply).Where(IsTrue).ToList());

    /// <summary>
    /// <c>compareparts</c>: the sides joined, each <c>%alternatives[a,b]</c> on the expected side
    /// matching either choice.
    /// </summary>
    public static bool Match(List<string> actual, List<string> expected) =>
        Match(string.Concat(actual), string.Concat(expected));

    /// <summary>
    /// The expected side with each <c>%alternatives[a,b]</c> shown as its first choice.
    /// </summary>
    public static string FirstAlternatives(string expected) => Alternatives.Replace(expected, "$1");

    /// <summary>
    /// Text split after each line feed.
    /// </summary>
    public static List<string> Split(string text)
    {
        var lines = new List<string>();
        var start = 0;

        while (start < text.Length)
        {
            var end = text.IndexOf('\n', start);
            var next = end < 0 ? text.Length : end + 1;
            lines.Add(text[start..next]);
            start = next;
        }

        return lines;
    }

    /// <summary>
    /// Perl's <c>chomp</c> of one line.
    /// </summary>
    public static string Chomp(string line) => line.EndsWith('\n') ? line[..^1] : line;

    private static UpstreamTestCaseStripLine ReadStripLine(string line) =>
        UpstreamTestCaseStripLine.TryParse(line)
        ?? throw new NotSupportedException($"The strip line '{line}' is outside ADR-0080 decision 4's subset.");

    private static bool Match(string actual, string expected)
    {
        var alternative = Alternatives.Match(expected);

        if (!alternative.Success)
        {
            return actual == expected;
        }

        var before = expected[..alternative.Index];
        var after = expected[(alternative.Index + alternative.Length)..];
        return Match(actual, before + alternative.Groups[1].Value + after)
            || Match(actual, before + alternative.Groups[2].Value + after);
    }
}
