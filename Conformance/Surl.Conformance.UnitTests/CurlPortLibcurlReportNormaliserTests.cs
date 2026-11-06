namespace Surl.Conformance;

[TestClass]
public sealed class CurlPortLibcurlReportNormaliserTests
{
    private const string Ok = "CURLcode 0 (No error)";

    [TestMethod]
    public void NormaliseLine_SurlPortInAUrl_IsReplaced()
    {
        var line = NormaliseLine("setopt RTSP_STREAM_URI \"rtsp://127.0.0.1:50123/clip.bin\": CURLcode 0 (No error)");

        Assert.AreEqual("setopt RTSP_STREAM_URI \"rtsp://127.0.0.1:<port>/clip.bin\": CURLcode 0 (No error)", line);
    }

    [TestMethod]
    public void NormaliseLine_LongerNumberStartingWithThePort_IsKept()
    {
        Assert.AreEqual("x :501234", NormaliseLine("x :501234"));
    }

    [TestMethod]
    public void NormaliseLine_SendInSeveralCalls_DropsTheCallCount()
    {
        var line = NormaliseLine($"send BINARY 65 bytes \"aa...\": {Ok}, sent 65 in 4 calls");

        Assert.AreEqual($"send BINARY 65 bytes \"aa...\": {Ok}, sent 65", line);
    }

    [TestMethod]
    public void NormaliseLine_SessionSurlDrew_IsReplaced()
    {
        var line = NormaliseLine($"SETUP: {Ok}, status 200, session \"4A98BFB49ED4CCE2\", cseq received 1, next cseq 2, body 0 bytes \"\", interleaved none");

        Assert.AreEqual($"SETUP: {Ok}, status 200, session \"<session>\", cseq received 1, next cseq 2, body 0 bytes \"\", interleaved none", line);
    }

    [TestMethod]
    public void NormaliseLine_SessionTheScriptSet_IsKept()
    {
        var line = NormaliseLine("setopt RTSP_SESSION_ID \"DEADBEEF\": CURLcode 0 (No error)");

        Assert.AreEqual("setopt RTSP_SESSION_ID \"DEADBEEF\": CURLcode 0 (No error)", line);
    }

    [TestMethod]
    public void NormaliseLine_InterleavedFrames_ZeroesTheirRandomFieldsButNotTheBody()
    {
        byte[] rtp = [(byte)'$', 0, 0, 14, 0x80, 0xE0, 0x12, 0x34, 1, 2, 3, 4, 5, 6, 7, 8, 0xAA, 0xBB];
        byte[] body = [(byte)'$', 0, 0, 1, 0x80, 0xE0, 0x12, 0x34];
        var line = $"RECEIVE: {Ok}, status 0, session none, cseq received 2, next cseq 3, body {LibcurlBytes.Describe(body)}, interleaved 1 calls: {LibcurlBytes.Describe(rtp)}";

        var normalised = NormaliseLine(line);

        byte[] masked = [(byte)'$', 0, 0, 14, 0x80, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xAA, 0xBB];
        Assert.AreEqual($"RECEIVE: {Ok}, status 0, session none, cseq received 2, next cseq 3, body {LibcurlBytes.Describe(body)}, interleaved 1 calls: {LibcurlBytes.Describe(masked)}", normalised);
    }

    [TestMethod]
    public void NormaliseLine_NullLine_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortLibcurlReportNormaliser.NormaliseLine(null!, 1));
    }

    [TestMethod]
    public void MaskInterleavedFrame_SenderReportAndBye_ZeroesTheSsrcsAndTimestampsAndKeepsTheCounts()
    {
        byte[] frame =
        [
            (byte)'$', 1, 0, 36,
            0x80, 0xC8, 0x00, 0x06, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 0, 0, 0, 1, 0, 0, 0, 100,
            0x81, 0xCB, 0x00, 0x01, 1, 1, 1, 1,
        ];

        var masked = CurlPortLibcurlReportNormaliser.MaskInterleavedFrame(frame);

        byte[] expected =
        [
            (byte)'$', 1, 0, 36,
            0x80, 0xC8, 0x00, 0x06, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 100,
            0x81, 0xCB, 0x00, 0x01, 0, 0, 0, 0,
        ];
        CollectionAssert.AreEqual(expected, masked);
        Assert.AreEqual(1, frame[8], "The frame given is not changed.");
    }

    [TestMethod]
    public void MaskInterleavedFrame_OtherRtcpPacket_ZeroesOnlyItsSsrc()
    {
        byte[] frame = [(byte)'$', 1, 0, 12, 0x81, 0xCA, 0x00, 0x02, 1, 1, 1, 1, 9, 9, 9, 9];

        var masked = CurlPortLibcurlReportNormaliser.MaskInterleavedFrame(frame);

        CollectionAssert.AreEqual(new byte[] { (byte)'$', 1, 0, 12, 0x81, 0xCA, 0x00, 0x02, 0, 0, 0, 0, 9, 9, 9, 9 }, masked);
    }

    [TestMethod]
    public void MaskInterleavedFrame_RtpCutShort_IsZeroedAsFarAsItGoes()
    {
        byte[] frame = [(byte)'$', 0, 0, 12, 0x80, 0x60, 7, 7, 7];

        var masked = CurlPortLibcurlReportNormaliser.MaskInterleavedFrame(frame);

        CollectionAssert.AreEqual(new byte[] { (byte)'$', 0, 0, 12, 0x80, 0x60, 0, 0, 0 }, masked);
    }

    [TestMethod]
    [DataRow(new byte[] { (byte)'$', 0, 0, 1, 0x80 })]
    [DataRow(new byte[] { (byte)'#', 0, 0, 12, 0x80, 0x60, 7, 7 })]
    public void MaskInterleavedFrame_NotAnInterleavedFrame_IsUnchanged(byte[] bytes)
    {
        CollectionAssert.AreEqual(bytes, CurlPortLibcurlReportNormaliser.MaskInterleavedFrame(bytes));
    }

    [TestMethod]
    public void MaskInterleavedFrame_Null_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortLibcurlReportNormaliser.MaskInterleavedFrame(null!));
    }

    private static string NormaliseLine(string line) => CurlPortLibcurlReportNormaliser.NormaliseLine(line, 50123);
}
