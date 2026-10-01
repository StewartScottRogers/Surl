namespace Surl.Conformance;

[TestClass]
public sealed class LibcurlRtspScriptTests
{
    [TestMethod]
    public void Parse_UrlAndEveryRequest_ReadsThemInOrderWithTheDefaults()
    {
        string[] requests = ["OPTIONS", "DESCRIBE", "ANNOUNCE", "SETUP", "PLAY", "PAUSE", "TEARDOWN", "GET_PARAMETER", "SET_PARAMETER", "RECORD", "RECEIVE"];

        var script = LibcurlRtspScript.Parse(["rtsp://127.0.0.1:18554/clip", .. requests]);

        Assert.IsNull(script.LibraryPath);
        Assert.AreEqual(TimeSpan.FromSeconds(10), script.Timeout);
        Assert.AreEqual("rtsp://127.0.0.1:18554/clip", script.Url);
        CollectionAssert.AreEqual(
            Enumerable.Range(1, 11).Select(value => (LibcurlRtspRequest)value).ToList(),
            script.Steps.Select(step => step.Request).ToList());
        Assert.IsTrue(script.Steps.All(step => step.Kind == LibcurlRtspStepKind.Request && step.Value.Length == 0));
    }

    [TestMethod]
    public void Parse_LibraryAndTimeout_ReadsBoth()
    {
        var script = LibcurlRtspScript.Parse(["--library", "/pinned/libcurl-4.dll", "--timeout", "2500", "rtsp://h/"]);

        Assert.AreEqual("/pinned/libcurl-4.dll", script.LibraryPath);
        Assert.AreEqual(TimeSpan.FromMilliseconds(2500), script.Timeout);
        Assert.AreEqual(0, script.Steps.Count);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("-5")]
    [DataRow("soon")]
    public void Parse_TimeoutNotAPositiveNumber_Refuses(string timeout)
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => LibcurlRtspScript.Parse(["--timeout", timeout, "rtsp://h/"]));

        StringAssert.Contains(exception.Message, "--timeout");
    }

    [TestMethod]
    public void Parse_OptionWithoutItsValue_Refuses()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => LibcurlRtspScript.Parse(["--library"]));

        Assert.AreEqual("--library needs a value.", exception.Message);
    }

    [TestMethod]
    public void Parse_NoUrl_Refuses()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => LibcurlRtspScript.Parse(["--timeout", "5"]));

        StringAssert.Contains(exception.Message, "rtsp:// URL is required");
    }

    [TestMethod]
    [DataRow("stream-uri:rtsp://h/clip/track1", LibcurlRtspStepKind.StreamUri, "rtsp://h/clip/track1")]
    [DataRow("transport:RTP/AVP;unicast;client_port=4588-4589", LibcurlRtspStepKind.Transport, "RTP/AVP;unicast;client_port=4588-4589")]
    [DataRow("session-id:12345678", LibcurlRtspStepKind.SessionId, "12345678")]
    [DataRow("body:position\\r\\n", LibcurlRtspStepKind.PostFields, "position\r\n")]
    [DataRow("upload:v=0\\r\\n", LibcurlRtspStepKind.Upload, "v=0\r\n")]
    [DataRow("transport:", LibcurlRtspStepKind.Transport, "")]
    public void ParseStep_ValueOption_ReadsItsUnescapedBytes(string text, LibcurlRtspStepKind kind, string value)
    {
        var step = LibcurlRtspScript.ParseStep(text);

        Assert.AreEqual(kind, step.Kind);
        CollectionAssert.AreEqual(value.Select(character => (byte)character).ToArray(), step.Value);
        Assert.AreEqual(0, step.Number);
    }

    [TestMethod]
    public void ParseStep_ClientCSeq_ReadsTheNumber()
    {
        var step = LibcurlRtspScript.ParseStep("client-cseq:42");

        Assert.AreEqual(LibcurlRtspStepKind.ClientCSeq, step.Kind);
        Assert.AreEqual(42, step.Number);
        Assert.AreEqual(0, step.Value.Length);
    }

    [TestMethod]
    public void ParseStep_ClientCSeqNotAPositiveNumber_Refuses()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => LibcurlRtspScript.ParseStep("client-cseq:0"));

        Assert.AreEqual("step 'client-cseq:0' needs a positive number.", exception.Message);
    }

    [TestMethod]
    public void ParseStep_NoBody_IsTheNoBodyStep()
    {
        var step = LibcurlRtspScript.ParseStep("no-body");

        Assert.AreEqual(LibcurlRtspStepKind.NoBody, step.Kind);
    }

    [TestMethod]
    [DataRow("options")]
    [DataRow("PUT")]
    [DataRow("session:1")]
    [DataRow("no-body:x")]
    public void ParseStep_Unknown_RefusesNamingWhatIsAllowed(string text)
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => LibcurlRtspScript.ParseStep(text));

        StringAssert.StartsWith(exception.Message, $"step '{text}' is neither a request (OPTIONS, DESCRIBE,");
        StringAssert.Contains(exception.Message, "client-cseq:<value>");
    }
}
