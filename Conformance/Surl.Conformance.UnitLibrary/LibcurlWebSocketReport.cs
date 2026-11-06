namespace Surl.Conformance;

/// <summary>
/// The lines a libcurl WebSocket driver prints, one per call - the same from
/// <c>Run-LibcurlWebSocketScript.cs</c> and its Curl port twin, so the two reports compare line by line
/// (ADR-0089 decision 1 (c)). Each <c>result</c> is the call's <c>CURLcode &lt;n&gt; (&lt;text&gt;)</c>.
/// </summary>
public static class LibcurlWebSocketReport
{
    private const int LongestPayloadShownInSendLine = 64;

    /// <summary>
    /// The line of the opening <c>curl_easy_perform</c>: <c>perform: CURLcode 0 (No error)</c>.
    /// </summary>
    /// <param name="result">What <c>curl_easy_perform</c> returned.</param>
    /// <returns>The line.</returns>
    public static string PerformLine(string result) => $"perform: {result}";

    /// <summary>
    /// The line of one send step, such as <c>send TEXT 5 bytes "hello": CURLcode 0 (No error), sent 5</c>;
    /// a payload past 64 bytes is shown to its 64th byte and <c>...</c>, and a step that took more than
    /// one <c>curl_ws_send</c> says in how many calls.
    /// </summary>
    /// <param name="step">The send step.</param>
    /// <param name="result">What the last <c>curl_ws_send</c> returned.</param>
    /// <param name="sent">How many bytes of the payload were sent in all.</param>
    /// <param name="calls">How many <c>curl_ws_send</c> calls the step took.</param>
    /// <returns>The line.</returns>
    public static string SendLine(LibcurlWebSocketStep step, string result, int sent, int calls)
    {
        ArgumentNullException.ThrowIfNull(step);

        var payload = step.Payload;
        var shown = payload.Length > LongestPayloadShownInSendLine
            ? $"{LibcurlBytes.Show(payload.AsSpan(0, LongestPayloadShownInSendLine))}...\""
            : $"{LibcurlBytes.Show(payload)}\"";
        var inCalls = calls > 1 ? $" in {calls} calls" : string.Empty;
        return $"send {LibcurlWebSocketFlags.NamesOf(step.Flags)} {payload.Length} bytes \"{shown}: {result}, sent {sent}{inCalls}";
    }

    /// <summary>
    /// The line of a <c>curl_ws_recv</c> that delivered a frame's bytes, such as
    /// <c>recv: CURLcode 0 (No error), flags TEXT, offset 0, bytesleft 0, 5 bytes "hello"</c>.
    /// </summary>
    /// <param name="result">What <c>curl_ws_recv</c> returned.</param>
    /// <param name="flags">The frame's <c>CURLWS_*</c> bits.</param>
    /// <param name="offset">The frame's <c>offset</c>.</param>
    /// <param name="bytesLeft">The frame's <c>bytesleft</c>.</param>
    /// <param name="payload">The bytes received.</param>
    /// <returns>The line.</returns>
    public static string ReceiveLine(string result, uint flags, long offset, long bytesLeft, ReadOnlySpan<byte> payload) =>
        $"recv: {result}, flags {LibcurlWebSocketFlags.NamesOf(flags)}, offset {offset}, bytesleft {bytesLeft}, {payload.Length} bytes \"{LibcurlBytes.Show(payload)}\"";

    /// <summary>
    /// The line of a <c>curl_ws_recv</c> that delivered no frame: <c>recv: CURLcode 52 (...)</c>.
    /// </summary>
    /// <param name="result">What <c>curl_ws_recv</c> returned.</param>
    /// <returns>The line.</returns>
    public static string ReceiveFailedLine(string result) => $"recv: {result}";
}
