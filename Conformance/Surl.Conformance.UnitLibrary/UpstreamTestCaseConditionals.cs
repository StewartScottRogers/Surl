using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// The <c>%if feature</c>, <c>%if !feature</c>, <c>%else</c> and <c>%endif</c> lines of a test
/// case, nested, deciding which lines are kept - the state machine of upstream's
/// <c>prepro</c> (<c>tests/runner.pm</c> at tag <c>curl-8_21_0</c>).
/// </summary>
/// <param name="features">The features present.</param>
public sealed class UpstreamTestCaseConditionals(IReadOnlySet<string> features)
{
    private readonly Stack<bool> enclosingShows = new();
    private readonly Stack<bool> alternativeShows = new();

    /// <summary>
    /// Whether the lines at this point are kept.
    /// </summary>
    public bool Showing { get; private set; } = true;

    /// <summary>
    /// Reads one source line: a conditional line changes <see cref="Showing"/> and is consumed,
    /// any other line is left to the caller.
    /// </summary>
    /// <param name="line">The source line.</param>
    /// <param name="lineNumber">Its one-based number, for the error.</param>
    /// <returns><see langword="true"/> when the line was a conditional line.</returns>
    /// <exception cref="UpstreamTestCaseFormatException">An <c>%else</c> or <c>%endif</c> has no <c>%if</c>.</exception>
    public bool Consume(string line, int lineNumber)
    {
        ArgumentNullException.ThrowIfNull(line);

        var condition = If.Match(line);
        if (condition.Success)
        {
            Open(condition.Groups[1].Value);
            return true;
        }

        if (Else.IsMatch(line))
        {
            RequireOpen("%else", lineNumber);
            Showing = alternativeShows.Peek();
            return true;
        }

        if (EndIf.IsMatch(line))
        {
            RequireOpen("%endif", lineNumber);
            Showing = enclosingShows.Pop();
            alternativeShows.Pop();
            return true;
        }

        return false;
    }

    private void Open(string condition)
    {
        var inverted = condition.StartsWith('!');
        var feature = inverted ? condition[1..] : condition;
        var holds = features.Contains(feature) != inverted;

        enclosingShows.Push(Showing);
        alternativeShows.Push(Showing && !holds);
        Showing = Showing && holds;
    }

    private void RequireOpen(string instruction, int lineNumber)
    {
        if (enclosingShows.Count == 0)
        {
            throw new UpstreamTestCaseFormatException(lineNumber, $"{instruction} with no %if");
        }
    }

    private static readonly Regex If = new("^ *%if ([A-Za-z0-9!_-]*)");

    private static readonly Regex Else = new("^ *%else");

    private static readonly Regex EndIf = new("^ *%endif");
}
