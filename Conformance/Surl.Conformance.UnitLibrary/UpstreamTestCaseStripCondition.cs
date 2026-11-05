using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// The condition of a <c>$_ = '' if &lt;condition&gt;;</c> strip line (ADR-0080 decision 4):
/// <c>/regex/</c> or <c>$_ =~ /regex/</c> (the line matches), <c>$_ !~ /regex/</c> (it does not),
/// parentheses, <c>and</c>/<c>&amp;&amp;</c>, <c>or</c>/<c>||</c> and <c>not</c>/<c>!</c>.
/// </summary>
/// <remarks>
/// <c>or</c> and <c>||</c> bind loosest, then <c>and</c> and <c>&amp;&amp;</c>, then the
/// negations, each applying to the operand after it. Perl ranks the word forms below the symbol
/// forms; the two orders differ only where the forms are mixed without parentheses, which no case
/// at tag <c>curl-8_21_0</c> does.
/// </remarks>
internal sealed class UpstreamTestCaseStripCondition
{
    private static readonly string[] Symbols = ["||", "&&", "=~", "!~", "$_", "(", ")", "!"];

    private static readonly Regex Word = new("^[a-z]+", RegexOptions.CultureInvariant);

    private readonly List<Token> tokens;

    private int position;

    private UpstreamTestCaseStripCondition(List<Token> tokens) => this.tokens = tokens;

    /// <summary>
    /// Parses a condition into a test of one line.
    /// </summary>
    /// <param name="text">The condition, as written after <c>if</c>.</param>
    /// <returns>The test, or <see langword="null"/> when the condition is outside the subset.</returns>
    public static Func<string, bool>? TryParse(string text)
    {
        var tokens = Tokenize(text);

        if (tokens is null)
        {
            return null;
        }

        var parser = new UpstreamTestCaseStripCondition(tokens);
        var condition = parser.ParseOr();
        return parser.position == tokens.Count ? condition : null;
    }

    private static List<Token>? Tokenize(string text)
    {
        var tokens = new List<Token>();
        var index = 0;

        while (index < text.Length)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                index++;
                continue;
            }

            var token = ReadToken(text, ref index);

            if (token is null)
            {
                return null;
            }

            tokens.Add(token);
        }

        return tokens;
    }

    private static Token? ReadToken(string text, ref int index)
    {
        if (text[index] == '/')
        {
            return ReadRegex(text, ref index);
        }

        var start = index;
        var symbol = Symbols.FirstOrDefault(candidate => string.CompareOrdinal(text, start, candidate, 0, candidate.Length) == 0);

        if (symbol is not null)
        {
            index += symbol.Length;
            return new Token(symbol, null);
        }

        var word = Word.Match(text[index..]).Value;
        index += word.Length;
        return word is "and" or "or" or "not" ? new Token(word, null) : null;
    }

    private static Token? ReadRegex(string text, ref int index)
    {
        var end = index + 1;

        while (end < text.Length && text[end] != '/')
        {
            end += text[end] == '\\' ? 2 : 1;
        }

        if (end >= text.Length)
        {
            return null;
        }

        var regex = UpstreamTestCasePerlRegex.TryCreate(text[(index + 1)..end], ignoreCase: false);
        index = end + 1;
        return regex is null ? null : new Token("/", regex);
    }

    private bool Accept(params string[] kinds)
    {
        if (position < tokens.Count && kinds.Contains(tokens[position].Kind))
        {
            position++;
            return true;
        }

        return false;
    }

    private Func<string, bool>? ParseOr()
    {
        var left = ParseAnd();

        while (left is not null && Accept("or", "||"))
        {
            var first = left;
            var right = ParseAnd();
            left = right is null ? null : line => first(line) || right(line);
        }

        return left;
    }

    private Func<string, bool>? ParseAnd()
    {
        var left = ParseUnary();

        while (left is not null && Accept("and", "&&"))
        {
            var first = left;
            var right = ParseUnary();
            left = right is null ? null : line => first(line) && right(line);
        }

        return left;
    }

    private Func<string, bool>? ParseUnary()
    {
        if (Accept("not", "!"))
        {
            var operand = ParseUnary();
            return operand is null ? null : line => !operand(line);
        }

        return ParsePrimary();
    }

    private Func<string, bool>? ParsePrimary()
    {
        if (Accept("("))
        {
            var inner = ParseOr();
            return inner is not null && Accept(")") ? inner : null;
        }

        var negated = false;

        if (Accept("$_"))
        {
            negated = !Accept("=~");

            if (negated && !Accept("!~"))
            {
                return null;
            }
        }

        return ParseMatch(negated);
    }

    private Func<string, bool>? ParseMatch(bool negated)
    {
        if (position >= tokens.Count || tokens[position].Regex is not { } regex)
        {
            return null;
        }

        position++;
        return line => regex.IsMatch(line) != negated;
    }

    private sealed record Token(string Kind, Regex? Regex);
}
