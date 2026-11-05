namespace Surl.Conformance;

/// <summary>
/// An upstream curl test case file that cannot be preprocessed or parsed, naming the line of
/// the source file that stops it.
/// </summary>
public sealed class UpstreamTestCaseFormatException : FormatException
{
    /// <summary>
    /// Creates the exception for <paramref name="lineNumber"/> of the source file.
    /// </summary>
    /// <param name="lineNumber">The one-based line of the source file.</param>
    /// <param name="problem">What is wrong on that line.</param>
    public UpstreamTestCaseFormatException(int lineNumber, string problem)
        : this(lineNumber, problem, null)
    {
    }

    /// <summary>
    /// Creates the exception for <paramref name="lineNumber"/> of the source file, wrapping the
    /// error that caused it.
    /// </summary>
    /// <param name="lineNumber">The one-based line of the source file.</param>
    /// <param name="problem">What is wrong on that line.</param>
    /// <param name="innerException">The error that caused it, or <see langword="null"/>.</param>
    public UpstreamTestCaseFormatException(int lineNumber, string problem, Exception? innerException)
        : base($"line {lineNumber}: {problem}", innerException)
    {
        LineNumber = lineNumber;
        Problem = problem;
    }

    /// <summary>
    /// The one-based line of the source file that cannot be read.
    /// </summary>
    public int LineNumber { get; }

    /// <summary>
    /// What is wrong on that line.
    /// </summary>
    public string Problem { get; }
}
