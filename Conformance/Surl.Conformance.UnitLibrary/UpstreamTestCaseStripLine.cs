using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// One line of a test case's <c>&lt;strippart&gt;</c> or <c>&lt;stripfile&gt;</c>, read as the
/// subset of Perl ADR-0080 decision 4 interprets, and applied to one line of output the way
/// upstream's runtests <c>eval</c>s it with the line in <c>$_</c>.
/// </summary>
/// <remarks>
/// The subset: <c>s&lt;d&gt;pattern&lt;d&gt;replacement&lt;d&gt;flags</c> with any punctuation
/// delimiter but brackets, flags <c>g</c> and <c>i</c>, optionally followed by
/// <c>or $_ = ''</c> (the line goes when nothing was replaced); <c>$_ = '' if &lt;condition&gt;</c>
/// (<see cref="UpstreamTestCaseStripCondition"/>); either with a closing <c>;</c>. A blank line or
/// one starting with <c>#</c> does nothing. In a replacement, <c>$1</c> and <c>${1}</c> are groups
/// (an unmatched or absent group is empty), <c>\r</c>, <c>\n</c> and <c>\t</c> are CR, LF and tab,
/// and a backslash before any other punctuation stands for that character, as in a Perl
/// double-quoted string; any other <c>$</c>, <c>@</c> before a name, or backslash before a letter
/// or digit is unsupported.
/// </remarks>
public sealed class UpstreamTestCaseStripLine
{
    private static readonly Regex Substitution = new(
        @"^\s*s(?<d>[^\w\s\\(\[{<])(?<pattern>(?:\\.|(?!\k<d>).)*)\k<d>(?<replacement>(?:\\.|(?!\k<d>).)*)\k<d>(?<flags>[A-Za-z]*)\s*(?<clear>or\s+\$_\s*=\s*''\s*)?;?\s*\z",
        RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex Clearing = new(
        @"^\s*\$_\s*=\s*''\s*if\b\s*(?<condition>.*?)\s*;?\s*\z",
        RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex GroupReference = new(@"\G(?:\{(?<n>[0-9]{1,9})\}|(?<n>[0-9]{1,9}))", RegexOptions.CultureInvariant);

    private readonly Func<string, string> apply;

    private UpstreamTestCaseStripLine(Func<string, string> apply) => this.apply = apply;

    /// <summary>
    /// Reads a strip line.
    /// </summary>
    /// <param name="line">The line without its newline.</param>
    /// <returns>The operation, or <see langword="null"/> when the line is outside the subset.</returns>
    public static UpstreamTestCaseStripLine? TryParse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var trimmed = line.Trim();

        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
        {
            return new UpstreamTestCaseStripLine(static text => text);
        }

        var substitution = Substitution.Match(line);

        return substitution.Success ? FromSubstitution(substitution) : FromClearing(line);
    }

    /// <summary>
    /// Reports whether the verify checker interprets a strip line: for <c>&lt;strip&gt;</c>, a
    /// regular expression <see cref="UpstreamTestCasePerlRegex"/> takes; for every other strip
    /// part, a line <see cref="TryParse"/> reads. This is the selector's
    /// <see cref="UpstreamTestCaseSelectionRules.SupportsStripLine"/>.
    /// </summary>
    /// <param name="part">The part's name: <c>strip</c>, <c>strippart</c>, <c>stripfile</c>, <c>stripfile1</c> ...</param>
    /// <param name="line">The line without its newline.</param>
    /// <returns><see langword="true"/> when the line is interpreted.</returns>
    public static bool IsSupported(string part, string line)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(line);

        return part == "strip"
            ? UpstreamTestCasePerlRegex.TryCreate(line, ignoreCase: false) is not null
            : TryParse(line) is not null;
    }

    /// <summary>
    /// Applies the operation to one line of output, as runtests' <c>eval</c> leaves <c>$_</c>.
    /// </summary>
    /// <param name="line">The line with its newline, one character per byte.</param>
    /// <returns>The line afterwards; empty when the operation removed it.</returns>
    public string Apply(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return apply(line);
    }

    private static UpstreamTestCaseStripLine? FromClearing(string line)
    {
        var clearing = Clearing.Match(line);
        var condition = clearing.Success ? UpstreamTestCaseStripCondition.TryParse(clearing.Groups["condition"].Value) : null;
        return condition is null ? null : new UpstreamTestCaseStripLine(text => condition(text) ? string.Empty : text);
    }

    private static UpstreamTestCaseStripLine? FromSubstitution(Match substitution)
    {
        var flags = substitution.Groups["flags"].Value;

        var regex = flags.All(flag => flag is 'g' or 'i')
            ? UpstreamTestCasePerlRegex.TryCreate(substitution.Groups["pattern"].Value, flags.Contains('i'))
            : null;
        var replacement = ParseReplacement(substitution.Groups["replacement"].Value);

        if (regex is null || replacement is null)
        {
            return null;
        }

        var count = flags.Contains('g') ? -1 : 1;
        var clearWhenUnmatched = substitution.Groups["clear"].Success;

        return new UpstreamTestCaseStripLine(text =>
        {
            var replaced = false;
            var result = regex.Replace(text, match => { replaced = true; return Expand(replacement, match); }, count);
            return replaced || !clearWhenUnmatched ? result : string.Empty;
        });
    }

    private static string Expand(List<(string Literal, int Group)> replacement, Match match)
    {
        var text = new StringBuilder();

        foreach (var (literal, group) in replacement)
        {
            text.Append(group < 0 ? literal : match.Groups[group].Value);
        }

        return text.ToString();
    }

    private static List<(string Literal, int Group)>? ParseReplacement(string text)
    {
        var parts = new List<(string Literal, int Group)>();
        var index = 0;

        while (index < text.Length)
        {
            var part = text[index] switch
            {
                '\\' => ReadEscape(text, ref index),
                '$' => ReadGroup(text, ref index),
                '@' => ReadAt(text, ref index),
                _ => (text[index++].ToString(), -1),
            };

            if (part is null)
            {
                return null;
            }

            parts.Add(part.Value);
        }

        return parts;
    }

    private static (string, int)? ReadEscape(string text, ref int index)
    {
        if (index + 1 >= text.Length)
        {
            return null;
        }

        var escaped = text[index + 1];
        index += 2;

        return escaped switch
        {
            'r' => ("\r", -1),
            'n' => ("\n", -1),
            't' => ("\t", -1),
            _ when char.IsAsciiLetterOrDigit(escaped) => null,
            _ => (escaped.ToString(), -1),
        };
    }

    private static (string, int)? ReadGroup(string text, ref int index)
    {
        var reference = GroupReference.Match(text, index + 1);

        if (!reference.Success)
        {
            return null;
        }

        index += 1 + reference.Length;
        return (string.Empty, int.Parse(reference.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static (string, int)? ReadAt(string text, ref int index)
    {
        var interpolates = index + 1 < text.Length && StartsArrayInterpolation(text[index + 1]);
        index++;
        return interpolates ? null : ("@", -1);
    }

    private static bool StartsArrayInterpolation(char next) =>
        char.IsAsciiLetterOrDigit(next) || next is '_' or '{' or '$';
}
