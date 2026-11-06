using System.Text;

namespace Surl.Conformance;

/// <summary>
/// One part of a test case section - <c>&lt;data&gt;</c>, <c>&lt;command&gt;</c>,
/// <c>&lt;protocol&gt;</c> and the like - with its attributes and its preprocessed lines.
/// </summary>
/// <param name="Name">The tag name, such as <c>data</c> or <c>file1</c>.</param>
/// <param name="Attributes">Its attributes by name, values as written (no entities).</param>
/// <param name="Lines">The lines between its tags, each with its newline, one character per byte.</param>
public sealed record UpstreamTestCasePart(
    string Name,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyList<string> Lines)
{
    /// <summary>
    /// The part's bytes as the preprocessed file holds them, before <c>nonewline</c> and
    /// <c>crlf</c> (a <c>&lt;data&gt;</c> or <c>&lt;connect&gt;</c> part's <c>crlf</c> is
    /// already applied by preprocessing).
    /// </summary>
    public byte[] Content => Encoding.Latin1.GetBytes(string.Concat(Lines));

    /// <summary>
    /// The part's text with FILEFORMAT.md's <c>nonewline</c> and <c>crlf</c> attributes applied
    /// by the rule upstream's runtests (<c>tests/runtests.pl</c> and <c>tests/runner.pm</c> at tag
    /// <c>curl-8_21_0</c>) uses for this kind of part.
    /// </summary>
    /// <param name="rule">
    /// The rule: <see cref="UpstreamTestCaseNewlineRule.CutThenCrlf"/> for <c>&lt;stdout&gt;</c>,
    /// <c>&lt;stderr&gt;</c>, <c>&lt;protocol&gt;</c>, <c>&lt;proxy&gt;</c>, the reply's
    /// <c>&lt;data&gt;</c> and <c>&lt;datacheck&gt;</c>; <see cref="UpstreamTestCaseNewlineRule.CutThenForceCrlf"/>
    /// for the client's <c>&lt;file&gt;</c> and <c>&lt;stdin&gt;</c>;
    /// <see cref="UpstreamTestCaseNewlineRule.CrlfThenCut"/> for the verify section's <c>&lt;file&gt;</c>.
    /// </param>
    /// <returns>The bytes the part stands for.</returns>
    /// <remarks>
    /// An attribute counts as set as Perl's truthiness counts it: present, not empty and not
    /// <c>0</c>. <c>crlf="headers"</c> guesses the header lines with a fresh
    /// <see cref="UpstreamTestCaseNewlines"/>, where upstream carries the guess over from the last
    /// line it rewrote anywhere; that differs only for a part whose first line is empty.
    /// </remarks>
    public byte[] ContentWithNewlineAttributes(UpstreamTestCaseNewlineRule rule)
    {
        var lines = Lines.ToList();
        var crlfFirst = rule == UpstreamTestCaseNewlineRule.CrlfThenCut;

        if (crlfFirst)
        {
            lines = RewriteNewlines(lines, rule);
        }

        if (IsSet("nonewline") && lines.Count > 0)
        {
            lines[^1] = CutFinalNewline(lines[^1]);
        }

        if (!crlfFirst)
        {
            lines = RewriteNewlines(lines, rule);
        }

        return Encoding.Latin1.GetBytes(string.Concat(lines));
    }

    /// <summary>
    /// Reports whether the part has <paramref name="name"/> with exactly <paramref name="value"/>.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the attribute has that value.</returns>
    public bool HasAttribute(string name, string value) =>
        Attributes.TryGetValue(name, out var actual) && actual == value;

    /// <summary>
    /// Reports whether <paramref name="name"/> is set as upstream's Perl tests it: present, and
    /// neither empty nor <c>0</c>.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    /// <returns><see langword="true"/> when the attribute is set.</returns>
    public bool IsSet(string name) =>
        Attributes.TryGetValue(name, out var value) && value.Length > 0 && value != "0";

    private static string CutFinalNewline(string line) => line.EndsWith('\n') ? line[..^1] : line;

    private List<string> RewriteNewlines(List<string> lines, UpstreamTestCaseNewlineRule rule)
    {
        if (!IsSet("crlf"))
        {
            return lines;
        }

        if (Attributes["crlf"] == "headers" && rule != UpstreamTestCaseNewlineRule.CutThenForceCrlf)
        {
            var guesser = new UpstreamTestCaseNewlines();
            return lines.Select(guesser.GuessHeaderCrlf).ToList();
        }

        return lines.Select(UpstreamTestCaseNewlines.ForceCrlf).ToList();
    }
}
