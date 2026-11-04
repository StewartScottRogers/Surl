namespace Surl.Conformance;

/// <summary>
/// What one RTSP request step of <c>Run-LibcurlRtspScript.cs</c> got back from libcurl: the
/// <c>curl_easy_perform</c> result, what <c>curl_easy_getinfo</c> reports after it, and the bytes
/// libcurl handed the write and interleave callbacks.
/// </summary>
/// <param name="Result">The <c>CURLcode</c> as the driver describes it, e.g. <c>CURLcode 0 (No error)</c>.</param>
/// <param name="Status">The <c>CURLINFO_RESPONSE_CODE</c>.</param>
/// <param name="SessionId">The <c>CURLINFO_RTSP_SESSION_ID</c>, or <see langword="null"/> when libcurl holds none.</param>
/// <param name="CSeqReceived">The <c>CURLINFO_RTSP_CSEQ_RECV</c>.</param>
/// <param name="NextClientCSeq">The <c>CURLINFO_RTSP_CLIENT_CSEQ</c>: the CSeq libcurl sends next.</param>
/// <param name="Body">The response body, as the write callback received it.</param>
/// <param name="Interleaved">The bytes of each call of the interleave callback, in order.</param>
public sealed record LibcurlRtspOutcome(
    string Result,
    long Status,
    string? SessionId,
    long CSeqReceived,
    long NextClientCSeq,
    byte[] Body,
    IReadOnlyList<byte[]> Interleaved);
