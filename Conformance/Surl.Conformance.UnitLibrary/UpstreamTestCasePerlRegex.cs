using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// Turns a Perl regular expression from a test case's <c>&lt;strip&gt;</c>, <c>&lt;strippart&gt;</c>
/// or <c>&lt;stripfile&gt;</c> into a .NET <see cref="Regex"/> that matches the same Latin-1 lines
/// (ADR-0080 decision 4).
/// </summary>
/// <remarks>
/// Upstream's runtests matches byte strings, where <c>\d</c>, <c>\w</c> and <c>\s</c> match ASCII
/// only; .NET's match Unicode classes, which Latin-1 letters such as <c>é</c> belong to. So those
/// six escapes are written out as ASCII classes. The rest of Perl's syntax that the corpus uses is
/// also .NET's; a pattern .NET cannot compile (<c>\K</c>, <c>\Q</c>, <c>\x{...}</c>) and a negated
/// class escape inside brackets (<c>[\S]</c>) are unsupported.
/// </remarks>
public static class UpstreamTestCasePerlRegex
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(5);

    private static readonly Dictionary<char, string> OutsideClass = new()
    {
        ['d'] = "[0-9]",
        ['D'] = "[^0-9]",
        ['w'] = "[A-Za-z0-9_]",
        ['W'] = "[^A-Za-z0-9_]",
        ['s'] = @"[\t\n\x0B\f\r ]",
        ['S'] = @"[^\t\n\x0B\f\r ]",
    };

    private static readonly Dictionary<char, string?> InsideClass = new()
    {
        ['d'] = "0-9",
        ['D'] = null,
        ['w'] = "A-Za-z0-9_",
        ['W'] = null,
        ['s'] = @"\t\n\x0B\f\r ",
        ['S'] = null,
    };

    /// <summary>
    /// Creates the .NET regular expression for a Perl pattern.
    /// </summary>
    /// <param name="pattern">The pattern, as written between the delimiters.</param>
    /// <param name="ignoreCase">Whether Perl's <c>i</c> flag is set.</param>
    /// <returns>The regular expression, or <see langword="null"/> when the pattern is unsupported.</returns>
    public static Regex? TryCreate(string pattern, bool ignoreCase)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var translated = Translate(pattern);
        var options = RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);

        try
        {
            return translated is null ? null : new Regex(translated, options, MatchTimeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? Translate(string pattern)
    {
        var output = new StringBuilder(pattern.Length);
        var insideClass = false;

        for (var index = 0; index < pattern.Length; index++)
        {
            var character = pattern[index];

            if (character == '\\' && index + 1 < pattern.Length)
            {
                var escape = TranslateEscape(pattern[++index], insideClass);

                if (escape is null)
                {
                    return null;
                }

                output.Append(escape);
                continue;
            }

            insideClass = insideClass ? character != ']' : character == '[';
            output.Append(character);
        }

        return output.ToString();
    }

    private static string? TranslateEscape(char escape, bool insideClass)
    {
        if (insideClass)
        {
            return InsideClass.TryGetValue(escape, out var inside) ? inside : "\\" + escape;
        }

        return OutsideClass.TryGetValue(escape, out var outside) ? outside : "\\" + escape;
    }
}
