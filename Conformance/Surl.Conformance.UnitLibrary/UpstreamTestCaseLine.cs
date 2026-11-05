namespace Surl.Conformance;

/// <summary>
/// One line of a preprocessed test case and the line of the source file it came from.
/// </summary>
/// <param name="Text">The line with its newline, one character per byte (Latin-1).</param>
/// <param name="SourceLineNumber">The one-based line of the source file that produced it.</param>
public sealed record UpstreamTestCaseLine(string Text, int SourceLineNumber);
