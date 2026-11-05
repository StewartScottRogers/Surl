namespace Surl.Conformance;

/// <summary>
/// How upstream's runtests applies a part's <c>nonewline</c> and <c>crlf</c> attributes, which
/// depends on the kind of part (<c>tests/runtests.pl</c> and <c>tests/runner.pm</c> at tag
/// <c>curl-8_21_0</c>).
/// </summary>
public enum UpstreamTestCaseNewlineRule
{
    /// <summary>
    /// Cut the final newline, then rewrite newlines: <c>crlf="headers"</c> only the header lines,
    /// any other set value every line. <c>&lt;stdout&gt;</c>, <c>&lt;stderr&gt;</c>,
    /// <c>&lt;protocol&gt;</c>, <c>&lt;proxy&gt;</c>, the reply's <c>&lt;data&gt;</c> and
    /// <c>&lt;datacheck&gt;</c>.
    /// </summary>
    CutThenCrlf,

    /// <summary>
    /// Cut the final newline, then make every newline CRLF whatever <c>crlf</c>'s value: the
    /// client's <c>&lt;file&gt;</c> and <c>&lt;stdin&gt;</c>.
    /// </summary>
    CutThenForceCrlf,

    /// <summary>
    /// Rewrite newlines as <see cref="CutThenCrlf"/> does, then cut the final newline: the verify
    /// section's <c>&lt;file&gt;</c>.
    /// </summary>
    CrlfThenCut,
}
