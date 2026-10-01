namespace Surl.Conformance;

/// <summary>
/// The pinned upstream <c>libcurl-4.dll</c>, driven through <c>curl_ws_send</c> and
/// <c>curl_ws_recv</c> by <c>Run-LibcurlWebSocketScript.cs</c>, completes the exchanges only its
/// API can make against a live, in-process <c>surl --ws-echo</c>, with the results ADR-0071's
/// amendment 1 expects: each data message comes back whole, a <c>PING</c> gets its <c>PONG</c>, a
/// <c>CLOSE</c> 1000 its echo and surl's close, a 2 MiB message <c>CLOSE</c> 1009 and invalid
/// UTF-8 <c>CLOSE</c> 1007. Windows only (ADR-0071 decision 10); Inconclusive elsewhere and
/// where the pinned library is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class PinnedLibcurlTalksToSurlOverWebSocketTests
{
    private const string Performed = "perform: CURLcode 0 (No error)";

    // CURLE_GOT_NOTHING: curl_ws_recv once surl has closed the connection after its CLOSE.
    private const string ConnectionClosed = "recv: CURLcode 52 ";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Text_Echo_ComesBackWhole()
    {
        var lines = await RunAgainstEchoAsync("send:TEXT:hello", "recv");

        Assert.AreEqual(Received("TEXT", "5 bytes \"hello\""), lines[2]);
    }

    [TestMethod]
    public async Task Binary_Echo_ComesBackWhole()
    {
        var lines = await RunAgainstEchoAsync(@"send:BINARY:\x00\x01\x02", "recv");

        Assert.AreEqual(Received("BINARY", "3 bytes \"\\x00\\x01\\x02\""), lines[2]);
    }

    [TestMethod]
    public async Task ThreeFragments_Echo_ComeBackAsOneMessage()
    {
        var lines = await RunAgainstEchoAsync("send:TEXT+CONT:hel", "send:TEXT+CONT:l", "send:TEXT:o", "recv");

        Assert.AreEqual(Received("TEXT", "5 bytes \"hello\""), lines[4]);
    }

    [TestMethod]
    public async Task Ping_Echo_IsAnsweredByAPongWithItsPayload()
    {
        var lines = await RunAgainstEchoAsync("send:PING:ping", "recv");

        Assert.AreEqual(Received("PONG", "4 bytes \"ping\""), lines[2]);
    }

    [TestMethod]
    public async Task Close1000_Echo_IsEchoedAndTheConnectionClosed()
    {
        var lines = await RunAgainstEchoAsync(@"send:CLOSE:\x03\xE8", "recv", "recv");

        Assert.AreEqual(Received("CLOSE", "2 bytes \"\\x03\\xE8\""), lines[2]);
        StringAssert.StartsWith(lines[3], ConnectionClosed);
    }

    [TestMethod]
    public async Task TwoMebibyteMessage_Echo_IsClosedWith1009()
    {
        var lines = await RunAgainstEchoWithDriverOptionsAsync(["--recv-timeout", "60000"], "send*2097152:BINARY:a", "recv", "recv");

        StringAssert.StartsWith(lines[1], "send BINARY 2097152 bytes ");
        StringAssert.Contains(lines[1], ": CURLcode 0 (No error), sent 2097152");
        Assert.AreEqual(Received("CLOSE", "2 bytes \"\\x03\\xF1\""), lines[2]);
        StringAssert.StartsWith(lines[3], ConnectionClosed);
    }

    [TestMethod]
    public async Task InvalidUtf8Text_Echo_IsClosedWith1007()
    {
        var lines = await RunAgainstEchoAsync(@"send:TEXT:\xFF\xFE", "recv", "recv");

        Assert.AreEqual(Received("CLOSE", "2 bytes \"\\x03\\xEF\""), lines[2]);
        StringAssert.StartsWith(lines[3], ConnectionClosed);
    }

    private static string Received(string flags, string payload) =>
        $"recv: CURLcode 0 (No error), flags {flags}, offset 0, bytesleft 0, {payload}";

    private Task<IReadOnlyList<string>> RunAgainstEchoAsync(params string[] steps) =>
        RunAgainstEchoWithDriverOptionsAsync([], steps);

    private async Task<IReadOnlyList<string>> RunAgainstEchoWithDriverOptionsAsync(string[] options, params string[] steps)
    {
        await using var surl = await SurlOnLoopback.StartInMemoryAsync("ws", ["--ws-echo"], TestContext.CancellationToken);

        var lines = await PinnedLibcurlWebSocketDriver.RunAsync(TestContext, [.. options, surl.UrlOf("echo"), .. steps]);

        Assert.AreEqual(Performed, lines[0], string.Join('\n', lines));
        Assert.HasCount(1 + steps.Length, lines, string.Join('\n', lines));
        return lines;
    }
}
