namespace Surl.Conformance;

[TestClass]
public sealed class LibcurlRtspReportTests
{
    private const string NoError = "CURLcode 0 (No error)";

    [TestMethod]
    [DataRow("stream-uri:rtsp://h/clip", "setopt RTSP_STREAM_URI \"rtsp://h/clip\": CURLcode 0 (No error)")]
    [DataRow("transport:RTP/AVP;unicast", "setopt RTSP_TRANSPORT \"RTP/AVP;unicast\": CURLcode 0 (No error)")]
    [DataRow("session-id:ab\\\"c", "setopt RTSP_SESSION_ID \"ab\\\"c\": CURLcode 0 (No error)")]
    [DataRow("client-cseq:7", "setopt RTSP_CLIENT_CSEQ 7: CURLcode 0 (No error)")]
    [DataRow("body:a\\r\\n", "setopt COPYPOSTFIELDS 3 bytes \"a\\x0D\\x0A\": CURLcode 0 (No error)")]
    [DataRow("upload:v=0", "setopt UPLOAD 3 bytes \"v=0\": CURLcode 0 (No error)")]
    [DataRow("no-body", "setopt no body (UPLOAD 0, POSTFIELDSIZE -1, POSTFIELDS NULL): CURLcode 0 (No error)")]
    public void SettingLine_EachOption_NamesItAndItsValue(string step, string line)
    {
        Assert.AreEqual(line, LibcurlRtspReport.SettingLine(LibcurlRtspScript.ParseStep(step), NoError));
    }

    [TestMethod]
    public void SettingLine_RequestStep_Refuses()
    {
        Assert.ThrowsExactly<ArgumentException>(() => LibcurlRtspReport.SettingLine(LibcurlRtspScript.ParseStep("PLAY"), NoError));
    }

    [TestMethod]
    public void RequestLine_NoSessionBodyOrInterleavedData_SaysNone()
    {
        var outcome = new LibcurlRtspOutcome(NoError, 200, null, 1, 2, [], []);

        Assert.AreEqual(
            "OPTIONS: CURLcode 0 (No error), status 200, session none, cseq received 1, next cseq 2, body 0 bytes \"\", interleaved none",
            LibcurlRtspReport.RequestLine(LibcurlRtspRequest.Options, outcome));
    }

    [TestMethod]
    public void RequestLine_SessionBodyAndInterleavedCalls_ShowsEach()
    {
        var outcome = new LibcurlRtspOutcome(
            "CURLcode 85 (RTSP CSeq mismatch or invalid CSeq)",
            454,
            "12345678",
            4,
            5,
            "v=0\r\n"u8.ToArray(),
            [[(byte)'$', 0, 0, 2, (byte)'h', (byte)'i'], [(byte)'$', 1, 0, 0]]);

        Assert.AreEqual(
            "GET_PARAMETER: CURLcode 85 (RTSP CSeq mismatch or invalid CSeq), status 454, session \"12345678\", cseq received 4, next cseq 5, "
                + "body 5 bytes \"v=0\\x0D\\x0A\", interleaved 2 calls: 6 bytes \"$\\x00\\x00\\x02hi\", 4 bytes \"$\\x01\\x00\\x00\"",
            LibcurlRtspReport.RequestLine(LibcurlRtspRequest.GetParameter, outcome));
    }
}
