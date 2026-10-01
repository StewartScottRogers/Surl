using System.Globalization;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// One request line <c>Run-LibcurlRtspScript.cs</c> printed (<see cref="LibcurlRtspReport.RequestLine"/>),
/// read back: the request, its <c>CURLcode</c>, status and session, the body and each interleave
/// call's bytes. A run of bytes the driver showed as a SHA-256 (past
/// <see cref="LibcurlBytes.LongestShown"/> bytes) fails the read, so a test keeps its frames short.
/// </summary>
/// <param name="Request">The request name, such as <c>PLAY</c>.</param>
/// <param name="CurlCode">The number of the <c>CURLcode</c> <c>curl_easy_perform</c> returned.</param>
/// <param name="Status">The <c>CURLINFO_RESPONSE_CODE</c>.</param>
/// <param name="SessionId">The <c>CURLINFO_RTSP_SESSION_ID</c>, or <see langword="null"/> for none.</param>
/// <param name="Body">The bytes the write callback received.</param>
/// <param name="Interleaved">The bytes of each interleave callback call, each a whole frame from <c>$</c> on.</param>
internal sealed partial record LibcurlRtspRequestLine(
    string Request, int CurlCode, long Status, string? SessionId, byte[] Body, IReadOnlyList<byte[]> Interleaved)
{
    /// <summary>
    /// Reads every request line of the driver's output, in order, skipping its <c>setopt</c> lines.
    /// </summary>
    public static IReadOnlyList<LibcurlRtspRequestLine> ReadAll(IReadOnlyList<string> lines) =>
        [.. lines.Where(line => !line.StartsWith("setopt ", StringComparison.Ordinal)).Select(Read)];

    private static LibcurlRtspRequestLine Read(string line)
    {
        var match = RequestLinePattern().Match(line);
        Assert.IsTrue(match.Success, $"Not a request line of the driver: {line}");

        var interleaved = match.Groups["interleaved"].Value;
        return new LibcurlRtspRequestLine(
            match.Groups["request"].Value,
            int.Parse(match.Groups["code"].Value, CultureInfo.InvariantCulture),
            long.Parse(match.Groups["status"].Value, CultureInfo.InvariantCulture),
            match.Groups["session"].Success ? match.Groups["session"].Value : null,
            ShownBytes(match.Groups["body"].Value),
            interleaved == "none" ? [] : [.. ShownRunPattern().Matches(interleaved).Select(run => ShownBytes(run.Value))]);
    }

    private static byte[] ShownBytes(string described)
    {
        var match = ShownRunPattern().Match(described);
        Assert.IsTrue(match.Success && match.Value == described && match.Groups["shown"].Success, $"Bytes not shown in full: {described}");
        return LibcurlBytes.Unescape(match.Groups["shown"].Value);
    }

    [GeneratedRegex("""^(?<request>[A-Z_]+): CURLcode (?<code>\d+) \(.*?\), status (?<status>\d+), session (?:none|"(?<session>[^"]*)"), cseq received \d+, next cseq \d+, body (?<body>\d+ bytes (?:"(?:[^"\\]|\\.)*"|sha256 [0-9A-F]+)), interleaved (?<interleaved>.*)$""")]
    private static partial Regex RequestLinePattern();

    [GeneratedRegex("""\d+ bytes (?:"(?<shown>(?:[^"\\]|\\.)*)"|sha256 [0-9A-F]+)""")]
    private static partial Regex ShownRunPattern();
}
