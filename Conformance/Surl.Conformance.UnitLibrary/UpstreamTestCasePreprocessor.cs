using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// Preprocesses an upstream curl test case file as FILEFORMAT.md's "Preprocessing" and
/// "Variables" define, in the order upstream's <c>prepro</c> (<c>tests/runner.pm</c> at tag
/// <c>curl-8_21_0</c>) applies them to each line.
/// </summary>
/// <remarks>
/// The source is read as upstream reads it, with CRLF made LF. For each line kept by the
/// <c>%if</c> lines: variables, <c>%includetext</c> (and variables again when it included
/// anything), the special characters, <c>%b64[]</c>, <c>%hex[]</c>, <c>%repeat[]</c>,
/// <c>%days[]</c>, <c>%include</c>, <c>%sha256b64file[]</c>, <c>%strippemfile[]</c>, and then,
/// inside a <c>&lt;data&gt;</c> or <c>&lt;connect&gt;</c> part (the prefix match upstream uses,
/// so <c>&lt;datacheck&gt;</c> and <c>&lt;data1&gt;</c> too) with a <c>crlf</c> attribute,
/// that attribute's newline rewriting - done here because the test servers read those parts
/// from the preprocessed file.
/// </remarks>
public static class UpstreamTestCasePreprocessor
{
    /// <summary>
    /// Preprocesses <paramref name="source"/>, the bytes of a test case file.
    /// </summary>
    /// <param name="source">The file's bytes.</param>
    /// <param name="environment">The variables, features, files and clock it depends on.</param>
    /// <returns>The preprocessed lines, each with the source line that produced it.</returns>
    /// <exception cref="UpstreamTestCaseFormatException">
    /// A conditional line has no <c>%if</c>, a file <c>%sha256b64file[]</c> or
    /// <c>%strippemfile[]</c> names cannot be read (a file <c>%include</c> names reads as empty,
    /// as upstream reads it), or a
    /// <c>%repeat[]</c> count is out of range.
    /// </exception>
    public static IReadOnlyList<UpstreamTestCaseLine> Preprocess(byte[] source, UpstreamTestCaseEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(environment);
        UpstreamTestCaseVariables.RequireKnownNames(environment.Variables);

        var conditionals = new UpstreamTestCaseConditionals(environment.Features);
        var instructions = new UpstreamTestCaseInstructions(environment.Files, environment.Time);
        var newlines = new UpstreamTestCaseNewlines();
        var crlf = string.Empty;
        var output = new List<UpstreamTestCaseLine>();

        var lines = SplitLines(Encoding.Latin1.GetString(source).Replace("\r\n", "\n", StringComparison.Ordinal));
        for (var index = 0; index < lines.Count; index++)
        {
            var lineNumber = index + 1;
            if (conditionals.Consume(lines[index], lineNumber) || !conditionals.Showing)
            {
                continue;
            }

            crlf = TrackDataCrlf(lines[index], crlf);
            var text = ExpandLine(lines[index], lineNumber, environment.Variables, instructions);
            text = RewriteNewlines(text, crlf, newlines);
            output.AddRange(SplitLines(text).Select(line => new UpstreamTestCaseLine(line, lineNumber)));
        }

        return Rejoin(output);
    }

    /// <summary>
    /// Preprocesses <paramref name="source"/> into the bytes upstream writes as
    /// <c>%LOGDIR/testNUM</c>.
    /// </summary>
    /// <param name="source">The file's bytes.</param>
    /// <param name="environment">The variables, features, files and clock it depends on.</param>
    /// <returns>The preprocessed file's bytes.</returns>
    /// <exception cref="UpstreamTestCaseFormatException">As <see cref="Preprocess"/>.</exception>
    public static byte[] PreprocessToBytes(byte[] source, UpstreamTestCaseEnvironment environment) =>
        Encoding.Latin1.GetBytes(string.Concat(Preprocess(source, environment).Select(line => line.Text)));

    /// <summary>
    /// Splits text after each line feed, each line keeping its own; the last line may have none.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The lines, none of them empty.</returns>
    public static List<string> SplitLines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lines = new List<string>();
        var start = 0;
        for (var end = text.IndexOf('\n', start); end >= 0; end = text.IndexOf('\n', start))
        {
            lines.Add(text[start..(end + 1)]);
            start = end + 1;
        }

        if (start < text.Length)
        {
            lines.Add(text[start..]);
        }

        return lines;
    }

    private static string TrackDataCrlf(string line, string crlf)
    {
        var opening = DataOrConnectOpening.Match(line);
        if (opening.Success)
        {
            return CrlfOf(opening.Groups[2].Value, crlf);
        }

        return DataOrConnectClosing.IsMatch(line) ? string.Empty : crlf;
    }

    private static string CrlfOf(string attributes, string crlf)
    {
        if (attributes.Contains("crlf=\"yes\"", StringComparison.Ordinal))
        {
            return "yes";
        }

        return attributes.Contains("crlf=\"headers\"", StringComparison.Ordinal) ? "headers" : crlf;
    }

    private static string ExpandLine(
        string line,
        int lineNumber,
        IReadOnlyDictionary<string, string> variables,
        UpstreamTestCaseInstructions instructions)
    {
        try
        {
            line = UpstreamTestCaseVariables.Substitute(line, variables);
            line = instructions.IncludeTextFiles(line, out var included);
            line = included ? UpstreamTestCaseVariables.Substitute(line, variables) : line;
            line = UpstreamTestCaseInstructions.ReplaceSpecialCharacters(line);
            line = instructions.ExpandEncodings(line);
            return instructions.ExpandFileDigests(line);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OverflowException or ArgumentException)
        {
            throw new UpstreamTestCaseFormatException(lineNumber, exception.Message, exception);
        }
    }

    private static string RewriteNewlines(string text, string crlf, UpstreamTestCaseNewlines newlines) => crlf switch
    {
        "yes" => UpstreamTestCaseNewlines.ForceCrlf(text),
        "headers" => newlines.GuessHeaderCrlf(text),
        _ => text,
    };

    private static List<UpstreamTestCaseLine> Rejoin(List<UpstreamTestCaseLine> pieces)
    {
        var lines = new List<UpstreamTestCaseLine>();
        foreach (var piece in pieces)
        {
            if (lines.Count > 0 && !lines[^1].Text.EndsWith('\n'))
            {
                lines[^1] = lines[^1] with { Text = lines[^1].Text + piece.Text };
                continue;
            }

            lines.Add(piece);
        }

        return lines;
    }

    private static readonly Regex DataOrConnectOpening = new("^ *<(data|connect)(.*)>");

    private static readonly Regex DataOrConnectClosing = new("^ *</(data|connect)");
}
