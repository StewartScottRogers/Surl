using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Phase 7's RTSP libcurl rows (ADR-0089 decision 1 (c), BL-597): each step list of
/// <see cref="PinnedLibcurlTalksToSurlOverRtspTests"/> run through <c>Run-LibcurlRtspScript.cs</c>, calling
/// the pinned upstream <c>libcurl-4.dll</c>, and through its Curl port twin <c>Run-CurlPortLibcurlRtspScript.cs</c>,
/// calling the port's pinned managed <c>Libcurl</c>, each against its own fresh <c>surl rtsp://</c> with the
/// mirrored test's options and content, judged by decision 5 against
/// <c>Documentation/Conformance/CurlPort/Libcurl.md</c>. Windows only; Inconclusive elsewhere and where either
/// pinned library is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortLibcurlBesideUpstreamLibcurlOverRtspTests
{
    private const string FindingsFile = "Libcurl.md";
    private const string Mirrored = nameof(PinnedLibcurlTalksToSurlOverRtspTests);
    private const string PlayTransport = "transport:RTP/AVP/TCP;interleaved=0-1";
    private const string RecordTransport = "transport:RTP/AVP/TCP;unicast;interleaved=0-1;mode=record";
    private const string Account = "tester:secret";
    private const string ClipUrl = $"rtsp://{CurlPortLibcurlCase.SurlAuthorityPlaceholder}/clip.bin";
    private const string RecordingUrl = $"rtsp://{CurlPortLibcurlCase.SurlAuthorityPlaceholder}/rec.bin";
    private const string LoggedInClipUrl = $"rtsp://{Account}@{CurlPortLibcurlCase.SurlAuthorityPlaceholder}/clip.bin";
    private const string LoggedInRecordingUrl = $"rtsp://{Account}@{CurlPortLibcurlCase.SurlAuthorityPlaceholder}/rec.bin";
    private const string AnnouncedFile = "rec.bin.sdp";
    private const string RecordedFile = "rec.bin";

    // The mirrored test's clip: one RTP frame of 100 bytes, shown in full by the driver.
    private static readonly byte[] Clip = [.. Enumerable.Range(0, 100).Select(index => (byte)(index * 7 % 251))];

    private static readonly Dictionary<string, byte[]> ServedFiles = new() { ["clip.bin"] = Clip };

    private static readonly string[] UploadOptions = ["--allow-uploads", "--user", Account, "--allow-plaintext-auth"];

    private static readonly string[] LoginOptions = ["--user", Account, "--allow-plaintext-auth"];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("Mirrors PinnedLibcurlTalksToSurlOverRtspTests.Describe_ServedFile_AnswersItsSessionDescription")]
    public Task Describe_ServedFile_AnswersItsSessionDescription() =>
        MeasureAsync(nameof(Describe_ServedFile_AnswersItsSessionDescription), [], ClipUrl, ClipUrl, ["DESCRIBE"], run =>
        {
            var requests = Requests(run);
            AssertAnswered(requests[0], 200);
            Assert.AreEqual(
                "v=0\r\no=- 0 0 IN IP4 127.0.0.1\r\ns=clip.bin\r\nc=IN IP4 0.0.0.0\r\nt=0 0\r\na=control:*\r\n"
                + "a=range:npt=0-\r\nm=application 0 RTP/AVP 96\r\na=rtpmap:96 octet-stream/90000\r\na=control:*\r\n",
                Encoding.ASCII.GetString(requests[0].Body));
            Assert.IsNull(requests[0].SessionId);
        });

    [TestMethod]
    [Description("Mirrors PinnedLibcurlTalksToSurlOverRtspTests.Setup_InterleavedTcp_AnswersA16HexDigitSession")]
    public Task Setup_InterleavedTcp_AnswersA16HexDigitSession() =>
        MeasureAsync(nameof(Setup_InterleavedTcp_AnswersA16HexDigitSession), [], ClipUrl, ClipUrl, [PlayTransport, "SETUP"], run =>
        {
            var requests = Requests(run);
            AssertAnswered(requests[0], 200);
            AssertIsSessionId(requests[0].SessionId);
        });

    [TestMethod]
    [Description("Mirrors PinnedLibcurlTalksToSurlOverRtspTests.PlayThenReceive_ServedFile_DeliversItsBytesThenAnRtcpBye")]
    public Task PlayThenReceive_ServedFile_DeliversItsBytesThenAnRtcpBye() =>
        MeasureAsync(nameof(PlayThenReceive_ServedFile_DeliversItsBytesThenAnRtcpBye), [], ClipUrl, ClipUrl, [PlayTransport, "SETUP", "PLAY", "RECEIVE", "RECEIVE", "RECEIVE"], run =>
        {
            var requests = Requests(run);
            AssertAnswered(requests[1], 200);
            AssertAnswered(requests[2], 0);
            var frames = requests.Skip(1).SelectMany(request => request.Interleaved).ToList();
            var rtp = frames.Where(frame => frame[1] == 0).ToList();
            CollectionAssert.AreEqual(Clip, rtp.SelectMany(frame => frame[16..]).ToArray());
            var ssrc = rtp[0][12..16];
            var rtcp = frames.Last(frame => frame[1] == 1);
            byte[] bye = [0x81, 0xCB, 0x00, 0x01, .. ssrc];
            CollectionAssert.AreEqual(bye, rtcp[^8..]);
        });

    [TestMethod]
    [Description("Mirrors PinnedLibcurlTalksToSurlOverRtspTests.PauseGetParameterTeardown_AfterPlay_AreEachAnsweredOnTheSameSession")]
    public Task PauseGetParameterTeardown_AfterPlay_AreEachAnsweredOnTheSameSession() =>
        MeasureAsync(nameof(PauseGetParameterTeardown_AfterPlay_AreEachAnsweredOnTheSameSession), [], ClipUrl, ClipUrl, [PlayTransport, "SETUP", "PLAY", "PAUSE", "GET_PARAMETER", "TEARDOWN"], run =>
        {
            var requests = Requests(run);
            var session = requests[0].SessionId;
            AssertIsSessionId(session);
            foreach (var request in requests.Skip(1))
            {
                AssertAnswered(request, 200);
                Assert.AreEqual(session, request.SessionId, request.Request);
            }
        });

    [TestMethod]
    [Description("Mirrors PinnedLibcurlTalksToSurlOverRtspTests.OptionsThenSetup_AfterTeardown_AreAnsweredAndSetUpTheSameSessionId")]
    public Task OptionsThenSetup_AfterTeardown_AreAnsweredAndSetUpTheSameSessionId() =>
        MeasureAsync(nameof(OptionsThenSetup_AfterTeardown_AreAnsweredAndSetUpTheSameSessionId), [], ClipUrl, ClipUrl, [PlayTransport, "SETUP", "PLAY", "TEARDOWN", "OPTIONS", "SETUP"], run =>
        {
            var requests = Requests(run);
            var session = requests[0].SessionId;
            AssertIsSessionId(session);
            AssertAnswered(requests[3], 200);
            AssertAnswered(requests[4], 200);
            Assert.AreEqual(session, requests[4].SessionId);
        });

    [TestMethod]
    [Description("Mirrors PinnedLibcurlTalksToSurlOverRtspTests.Setup_UdpTransport_Is461WithNoSession")]
    public Task Setup_UdpTransport_Is461WithNoSession() =>
        MeasureAsync(nameof(Setup_UdpTransport_Is461WithNoSession), [], ClipUrl, ClipUrl, ["transport:RTP/AVP;unicast;client_port=5000-5001", "SETUP"], run =>
        {
            var requests = Requests(run);
            AssertAnswered(requests[0], 461);
            Assert.IsNull(requests[0].SessionId);
        });

    [TestMethod]
    [Description("Mirrors PinnedLibcurlTalksToSurlOverRtspTests.Play_MadeUpSession_Is454")]
    public Task Play_MadeUpSession_Is454() =>
        MeasureAsync(nameof(Play_MadeUpSession_Is454), [], ClipUrl, ClipUrl, ["session-id:DEADBEEF", "PLAY"], run =>
            AssertAnswered(Requests(run)[0], 454));

    [TestMethod]
    [Description("Mirrors PinnedLibcurlTalksToSurlOverRtspTests.Record_SessionSetUpToPlayLoggedIn_Is455")]
    public Task Record_SessionSetUpToPlayLoggedIn_Is455() =>
        MeasureAsync(nameof(Record_SessionSetUpToPlayLoggedIn_Is455), UploadOptions, LoggedInClipUrl, ClipUrl, [PlayTransport, "SETUP", "RECORD"], run =>
            AssertAnswered(Requests(run)[1], 455));

    [TestMethod]
    [Description("Mirrors PinnedLibcurlTalksToSurlOverRtspTests.Record_NoLogin_Is401BeforeItsStateIsJudged")]
    public Task Record_NoLogin_Is401BeforeItsStateIsJudged() =>
        MeasureAsync(nameof(Record_NoLogin_Is401BeforeItsStateIsJudged), [], ClipUrl, ClipUrl, [PlayTransport, "SETUP", "RECORD"], run =>
            AssertAnswered(Requests(run)[1], 401));

    [TestMethod]
    [Description("Mirrors PinnedLibcurlTalksToSurlOverRtspTests.GetParameter_WithABody_Is451")]
    public Task GetParameter_WithABody_Is451() =>
        MeasureAsync(nameof(GetParameter_WithABody_Is451), [], ClipUrl, ClipUrl, [PlayTransport, "SETUP", @"body:volume\r\n", "GET_PARAMETER"], run =>
            AssertAnswered(Requests(run)[1], 451));

    [TestMethod]
    [Description("Mirrors PinnedLibcurlTalksToSurlOverRtspTests.Announce_UploadsOffLoggedIn_Is403AndStoresNothing")]
    public Task Announce_UploadsOffLoggedIn_Is403AndStoresNothing() =>
        MeasureAsync(nameof(Announce_UploadsOffLoggedIn_Is403AndStoresNothing), LoginOptions, LoggedInRecordingUrl, RecordingUrl, [@"body:v=0\r\n", "ANNOUNCE"], run =>
        {
            AssertAnswered(Requests(run)[0], 403);
            Assert.IsFalse(run.Surl.StoredFiles.ContainsKey(AnnouncedFile));
        });

    [TestMethod]
    [Description("Mirrors PinnedLibcurlTalksToSurlOverRtspTests.Announce_UploadsOnLoggedIn_StoresTheBodyBesideThePath")]
    public Task Announce_UploadsOnLoggedIn_StoresTheBodyBesideThePath() =>
        MeasureAsync(nameof(Announce_UploadsOnLoggedIn_StoresTheBodyBesideThePath), UploadOptions, LoggedInRecordingUrl, RecordingUrl, [@"body:v=0\r\n", "ANNOUNCE"], run =>
        {
            AssertAnswered(Requests(run)[0], 200);
            CollectionAssert.AreEqual("v=0\r\n"u8.ToArray(), run.Surl.StoredFiles.GetValueOrDefault(AnnouncedFile));
        });

    [TestMethod]
    [Description("Mirrors PinnedLibcurlTalksToSurlOverRtspTests.SetupRecordTeardown_UploadsOnLoggedIn_StoresAnEmptyRecording")]
    public Task SetupRecordTeardown_UploadsOnLoggedIn_StoresAnEmptyRecording() =>
        MeasureAsync(nameof(SetupRecordTeardown_UploadsOnLoggedIn_StoresAnEmptyRecording), UploadOptions, LoggedInRecordingUrl, RecordingUrl, ["no-body", RecordTransport, "SETUP", "RECORD", "TEARDOWN"], run =>
        {
            foreach (var request in Requests(run))
            {
                AssertAnswered(request, 200);
            }

            Assert.IsEmpty(run.Surl.StoredFiles.GetValueOrDefault(RecordedFile) ?? [1]);
        });

    private static IReadOnlyList<LibcurlRtspRequestLine> Requests(CurlPortLibcurlRun run) =>
        LibcurlRtspRequestLine.ReadAll(run.Lines);

    private static void AssertAnswered(LibcurlRtspRequestLine request, long status)
    {
        Assert.AreEqual(0, request.CurlCode, request.Request);
        Assert.AreEqual(status, request.Status, request.Request);
    }

    private static void AssertIsSessionId(string? sessionId)
    {
        Assert.IsNotNull(sessionId);
        Assert.AreEqual(16, sessionId.Length, sessionId);
        Assert.IsTrue(sessionId.All(Uri.IsHexDigit) && sessionId == sessionId.ToUpperInvariant(), sessionId);
    }

    // The mirrored test's driver arguments: a 3-second timeout, the URL (logged in where the mirrored test
    // logs in), the stream URI, then the steps; surl with the mirrored test's options over its clip.
    private async Task MeasureAsync(
        string name, string[] surlOptions, string url, string streamUri, string[] steps, Action<CurlPortLibcurlRun> assertions)
    {
        var libcurlCase = new CurlPortLibcurlCase(
            name,
            $"{Mirrored}.{name}",
            PinnedLibcurlDriver.RtspScript,
            ["--timeout", "3000", url, $"stream-uri:{streamUri}", .. steps],
            PinnedCurlPortLibcurlBesideUpstreamLibcurl.Asserting(assertions))
        {
            ComparedStoredFiles = [AnnouncedFile, RecordedFile],
        };

        var measurement = await PinnedCurlPortLibcurlBesideUpstreamLibcurl.MeasureAsync(
            TestContext, libcurlCase, cancellationToken => SurlOnLoopback.StartAsync("rtsp", ServedFiles, [], surlOptions, cancellationToken));

        await CurlPortFindingsFile.AssertVerdictIsListedAsync(TestContext, FindingsFile, name, measurement);
    }
}
