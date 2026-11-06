namespace Surl.Conformance;

/// <summary>
/// Phase 7's WebSocket libcurl rows (ADR-0089 decision 1 (c), BL-597): each step list of
/// <see cref="PinnedLibcurlTalksToSurlOverWebSocketTests"/> run through <c>Run-LibcurlWebSocketScript.cs</c>,
/// calling the pinned upstream <c>libcurl-4.dll</c>, and through its Curl port twin
/// <c>Run-CurlPortLibcurlWebSocketScript.cs</c>, calling the port's pinned managed <c>Libcurl</c>, each
/// against its own fresh <c>surl --ws-echo</c>, judged by decision 5 against
/// <c>Documentation/Conformance/CurlPort/Libcurl.md</c>. Windows only; Inconclusive elsewhere and where
/// either pinned library is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortLibcurlBesideUpstreamLibcurlOverWebSocketTests
{
    private const string FindingsFile = "Libcurl.md";
    private const string Performed = "perform: CURLcode 0 (No error)";
    private const string ConnectionClosed = "recv: CURLcode 52 ";
    private const string Mirrored = nameof(PinnedLibcurlTalksToSurlOverWebSocketTests);
    private const string EchoUrl = $"ws://{CurlPortLibcurlCase.SurlAuthorityPlaceholder}/echo";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("Mirrors PinnedLibcurlTalksToSurlOverWebSocketTests.Text_Echo_ComesBackWhole")]
    public Task Text_Echo_ComesBackWhole() =>
        MeasureAsync(nameof(Text_Echo_ComesBackWhole), [], ["send:TEXT:hello", "recv"], lines =>
            Assert.AreEqual(Received("TEXT", "5 bytes \"hello\""), lines[2]));

    [TestMethod]
    [Description("Mirrors PinnedLibcurlTalksToSurlOverWebSocketTests.Binary_Echo_ComesBackWhole")]
    public Task Binary_Echo_ComesBackWhole() =>
        MeasureAsync(nameof(Binary_Echo_ComesBackWhole), [], [@"send:BINARY:\x00\x01\x02", "recv"], lines =>
            Assert.AreEqual(Received("BINARY", "3 bytes \"\\x00\\x01\\x02\""), lines[2]));

    [TestMethod]
    [Description("Mirrors PinnedLibcurlTalksToSurlOverWebSocketTests.ThreeFragments_Echo_ComeBackAsOneMessage")]
    public Task ThreeFragments_Echo_ComeBackAsOneMessage() =>
        MeasureAsync(nameof(ThreeFragments_Echo_ComeBackAsOneMessage), [], ["send:TEXT+CONT:hel", "send:TEXT+CONT:l", "send:TEXT:o", "recv"], lines =>
            Assert.AreEqual(Received("TEXT", "5 bytes \"hello\""), lines[4]));

    [TestMethod]
    [Description("Mirrors PinnedLibcurlTalksToSurlOverWebSocketTests.Ping_Echo_IsAnsweredByAPongWithItsPayload")]
    public Task Ping_Echo_IsAnsweredByAPongWithItsPayload() =>
        MeasureAsync(nameof(Ping_Echo_IsAnsweredByAPongWithItsPayload), [], ["send:PING:ping", "recv"], lines =>
            Assert.AreEqual(Received("PONG", "4 bytes \"ping\""), lines[2]));

    [TestMethod]
    [Description("Mirrors PinnedLibcurlTalksToSurlOverWebSocketTests.Close1000_Echo_IsEchoedAndTheConnectionClosed")]
    public Task Close1000_Echo_IsEchoedAndTheConnectionClosed() =>
        MeasureAsync(nameof(Close1000_Echo_IsEchoedAndTheConnectionClosed), [], [@"send:CLOSE:\x03\xE8", "recv", "recv"], lines =>
        {
            Assert.AreEqual(Received("CLOSE", "2 bytes \"\\x03\\xE8\""), lines[2]);
            StringAssert.StartsWith(lines[3], ConnectionClosed);
        });

    [TestMethod]
    [Description("Mirrors PinnedLibcurlTalksToSurlOverWebSocketTests.TwoMebibyteMessage_Echo_IsClosedWith1009")]
    public Task TwoMebibyteMessage_Echo_IsClosedWith1009() =>
        MeasureAsync(nameof(TwoMebibyteMessage_Echo_IsClosedWith1009), ["--recv-timeout", "60000"], ["send*2097152:BINARY:a", "recv", "recv"], lines =>
        {
            StringAssert.StartsWith(lines[1], "send BINARY 2097152 bytes ");
            StringAssert.Contains(lines[1], ": CURLcode 0 (No error), sent 2097152");
            Assert.AreEqual(Received("CLOSE", "2 bytes \"\\x03\\xF1\""), lines[2]);
            StringAssert.StartsWith(lines[3], ConnectionClosed);
        });

    [TestMethod]
    [Description("Mirrors PinnedLibcurlTalksToSurlOverWebSocketTests.InvalidUtf8Text_Echo_IsClosedWith1007")]
    public Task InvalidUtf8Text_Echo_IsClosedWith1007() =>
        MeasureAsync(nameof(InvalidUtf8Text_Echo_IsClosedWith1007), [], [@"send:TEXT:\xFF\xFE", "recv", "recv"], lines =>
        {
            Assert.AreEqual(Received("CLOSE", "2 bytes \"\\x03\\xEF\""), lines[2]);
            StringAssert.StartsWith(lines[3], ConnectionClosed);
        });

    private static string Received(string flags, string payload) =>
        $"recv: CURLcode 0 (No error), flags {flags}, offset 0, bytesleft 0, {payload}";

    // The mirrored test's own checks of every run - the perform line and one line per step - then the
    // test's assertions.
    private async Task MeasureAsync(string name, string[] options, string[] steps, Action<IReadOnlyList<string>> assertions)
    {
        var libcurlCase = new CurlPortLibcurlCase(
            name,
            $"{Mirrored}.{name}",
            PinnedLibcurlDriver.WebSocketScript,
            [.. options, EchoUrl, .. steps],
            PinnedCurlPortLibcurlBesideUpstreamLibcurl.Asserting(run =>
            {
                Assert.HasCount(1 + steps.Length, run.Lines, string.Join('\n', run.Lines));
                Assert.AreEqual(Performed, run.Lines[0], string.Join('\n', run.Lines));
                assertions(run.Lines);
            }));

        var measurement = await PinnedCurlPortLibcurlBesideUpstreamLibcurl.MeasureAsync(
            TestContext, libcurlCase, cancellationToken => SurlOnLoopback.StartInMemoryAsync("ws", ["--ws-echo"], cancellationToken));

        await CurlPortFindingsFile.AssertVerdictIsListedAsync(TestContext, FindingsFile, name, measurement);
    }
}
