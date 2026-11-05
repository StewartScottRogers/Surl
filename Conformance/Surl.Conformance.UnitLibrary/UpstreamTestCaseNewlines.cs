using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// Upstream's <c>subnewlines</c> (<c>tests/testutil.pm</c> at tag <c>curl-8_21_0</c>): the
/// <c>crlf="yes"</c> and <c>crlf="headers"</c> rewriting of one line at a time.
/// </summary>
/// <remarks>
/// <c>crlf="yes"</c> turns the first run of carriage returns and a line feed in a line into one
/// CRLF. <c>crlf="headers"</c> does that only to a line that looks like an HTTP or RTSP status
/// line, request line or header, and to an empty line straight after one (the end of the
/// headers), so a body keeps its own newlines. The guess carries from line to line, so one
/// instance rewrites one sequence of lines. Perl's <c>\S</c> on raw bytes is written
/// <c>[^ \t\n\r\f\v]</c> here, because .NET's <c>\S</c> also excludes bytes 0x85 and 0xA0.
/// </remarks>
public sealed class UpstreamTestCaseNewlines
{
    private static readonly Regex FirstNewline = new("\r*\n");

    private static readonly Regex StatusLine = new(@"^HTTP/(1.1|1.0|2|3) ([1-5]|9)[^\r]*\z");

    private static readonly Regex HttpRequestLine =
        new(@"^(GET|HEAD|POST|PUT|DELETE|CONNECT) [^ \t\n\r\f\v]+ HTTP/\d+(\.\d+)?");

    private static readonly Regex RtspRequestLine =
        new(@"^(SETUP|GET_PARAMETER|OPTIONS|ANNOUNCE|DESCRIBE) [^ \t\n\r\f\v]+ RTSP/\d+(\.\d+)?");

    private static readonly Regex HeaderField =
        new(@"^[a-z0-9_-]+: [^\r]*\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CurlErrorMessage = new(@"^curl: \(\d+\) ");

    private bool previousLineRewritten;

    /// <summary>
    /// Rewrites the first newline of <paramref name="line"/> as CRLF, whatever the line holds.
    /// </summary>
    /// <param name="line">The line, one character per byte.</param>
    /// <returns>The line with its first <c>\r*\n</c> made <c>\r\n</c>.</returns>
    public static string ForceCrlf(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return FirstNewline.Replace(line, "\r\n", 1);
    }

    /// <summary>
    /// Rewrites <paramref name="line"/> as <c>crlf="headers"</c> does, given the lines this
    /// instance rewrote before it.
    /// </summary>
    /// <param name="line">The line, one character per byte.</param>
    /// <returns>The line, with CRLF if it is a header line or the empty line ending headers.</returns>
    public string GuessHeaderCrlf(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (IsHeaderLine(line))
        {
            previousLineRewritten = true;
            return ForceCrlf(line);
        }

        var endsHeaders = line == "\n" && previousLineRewritten;
        previousLineRewritten = false;
        return endsHeaders ? "\r\n" : line;
    }

    private static bool IsHeaderLine(string line) =>
        StatusLine.IsMatch(line)
        || HttpRequestLine.IsMatch(line)
        || RtspRequestLine.IsMatch(line)
        || (HeaderField.IsMatch(line) && !CurlErrorMessage.IsMatch(line));
}
