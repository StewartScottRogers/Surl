namespace Surl.Conformance;

[TestClass]
public sealed class LibcurlWebSocketReportTests
{
    private const string Ok = "CURLcode 0 (No error)";

    [TestMethod]
    public void PerformLine_Result_IsPrefixed()
    {
        Assert.AreEqual($"perform: {Ok}", LibcurlWebSocketReport.PerformLine(Ok));
    }

    [TestMethod]
    public void SendLine_ShortPayloadInOneCall_ShowsItWhole()
    {
        var step = LibcurlWebSocketScript.ParseStep("send:TEXT:hello");

        var line = LibcurlWebSocketReport.SendLine(step, Ok, 5, 1);

        Assert.AreEqual($"send TEXT 5 bytes \"hello\": {Ok}, sent 5", line);
    }

    [TestMethod]
    public void SendLine_LongPayloadInSeveralCalls_ShowsItsFirst64BytesAndTheCalls()
    {
        var step = LibcurlWebSocketScript.ParseStep("send*65:BINARY:a");

        var line = LibcurlWebSocketReport.SendLine(step, Ok, 65, 4);

        Assert.AreEqual($"send BINARY 65 bytes \"{new string('a', 64)}...\": {Ok}, sent 65 in 4 calls", line);
    }

    [TestMethod]
    public void SendLine_NullStep_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => LibcurlWebSocketReport.SendLine(null!, Ok, 0, 1));
    }

    [TestMethod]
    public void ReceiveLine_Frame_ShowsItsFlagsPositionAndBytes()
    {
        var line = LibcurlWebSocketReport.ReceiveLine(Ok, 8, 0, 0, [0x03, 0xE8]);

        Assert.AreEqual($"recv: {Ok}, flags CLOSE, offset 0, bytesleft 0, 2 bytes \"\\x03\\xE8\"", line);
    }

    [TestMethod]
    public void ReceiveFailedLine_Result_IsPrefixed()
    {
        Assert.AreEqual("recv: CURLcode 52 (x)", LibcurlWebSocketReport.ReceiveFailedLine("CURLcode 52 (x)"));
    }
}
