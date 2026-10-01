namespace Surl.Conformance;

/// <summary>
/// The lines <c>Run-LibcurlRtspScript.cs</c> writes to standard output, one per step, so
/// <c>Record-CurlExchange.ps1</c>'s <c>stdout.bin</c> says what libcurl made of each answer.
/// </summary>
public static class LibcurlRtspReport
{
    /// <summary>
    /// The line for an option step, e.g. <c>setopt RTSP_TRANSPORT "RTP/AVP;unicast": CURLcode 0 (No error)</c>.
    /// </summary>
    /// <param name="step">The option step; not a request.</param>
    /// <param name="result">The <c>CURLcode</c> of its <c>curl_easy_setopt</c> calls as the driver describes it: the first that failed, or the last.</param>
    /// <returns>The line.</returns>
    /// <exception cref="ArgumentException"><paramref name="step"/> is a request.</exception>
    public static string SettingLine(LibcurlRtspStep step, string result)
    {
        ArgumentNullException.ThrowIfNull(step);

        var setting = step.Kind switch
        {
            LibcurlRtspStepKind.StreamUri => $"RTSP_STREAM_URI \"{LibcurlBytes.Show(step.Value)}\"",
            LibcurlRtspStepKind.Transport => $"RTSP_TRANSPORT \"{LibcurlBytes.Show(step.Value)}\"",
            LibcurlRtspStepKind.SessionId => $"RTSP_SESSION_ID \"{LibcurlBytes.Show(step.Value)}\"",
            LibcurlRtspStepKind.ClientCSeq => $"RTSP_CLIENT_CSEQ {step.Number}",
            LibcurlRtspStepKind.PostFields => $"COPYPOSTFIELDS {LibcurlBytes.Describe(step.Value)}",
            LibcurlRtspStepKind.Upload => $"UPLOAD {LibcurlBytes.Describe(step.Value)}",
            LibcurlRtspStepKind.NoBody => "no body (UPLOAD 0, POSTFIELDSIZE -1, POSTFIELDS NULL)",
            _ => throw new ArgumentException("A request step has a request line, not a setting line.", nameof(step)),
        };

        return $"setopt {setting}: {result}";
    }

    /// <summary>
    /// The line for a request step, e.g.
    /// <c>DESCRIBE: CURLcode 0 (No error), status 200, session none, cseq received 2, next cseq 3, body 3 bytes "v=0", interleaved none</c>.
    /// </summary>
    /// <param name="request">The request the step performed.</param>
    /// <param name="outcome">What libcurl reported for it.</param>
    /// <returns>The line.</returns>
    public static string RequestLine(LibcurlRtspRequest request, LibcurlRtspOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        var name = LibcurlRtspScript.Requests.First(pair => pair.Value == request).Key;
        var session = outcome.SessionId is null ? "none" : $"\"{outcome.SessionId}\"";
        var interleaved = outcome.Interleaved.Count == 0
            ? "none"
            : $"{outcome.Interleaved.Count} calls: {string.Join(", ", outcome.Interleaved.Select(chunk => LibcurlBytes.Describe(chunk)))}";

        return $"{name}: {outcome.Result}, status {outcome.Status}, session {session}, cseq received {outcome.CSeqReceived}, "
            + $"next cseq {outcome.NextClientCSeq}, body {LibcurlBytes.Describe(outcome.Body)}, interleaved {interleaved}";
    }
}
