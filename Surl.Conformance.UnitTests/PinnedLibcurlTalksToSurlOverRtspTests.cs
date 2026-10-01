using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned upstream <c>libcurl-4.dll</c>, driven through <c>CURLOPT_RTSP_REQUEST</c> by
/// <c>Run-LibcurlRtspScript.cs</c>, completes the RTSP requests only its API sends against a live,
/// in-process <c>surl rtsp://</c>, with the results ADR-0074 amendment 1's last table expects:
/// <c>DESCRIBE</c>'s SDP, a session from <c>SETUP</c>, <c>PLAY</c>'s interleaved packets joining to
/// the file and its RTCP <c>BYE</c>, the session kept across <c>PAUSE</c>, <c>GET_PARAMETER</c>,
/// <c>TEARDOWN</c> and a second <c>SETUP</c>, each refusal's status, and <c>ANNOUNCE</c> and
/// <c>RECORD</c> storing their files. Every case sets <c>stream-uri:</c>. Windows only (ADR-0071
/// decision 10); Inconclusive elsewhere and where the pinned library is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class PinnedLibcurlTalksToSurlOverRtspTests
{
    private const string PlayTransport = "transport:RTP/AVP/TCP;interleaved=0-1";
    private const string RecordTransport = "transport:RTP/AVP/TCP;unicast;interleaved=0-1;mode=record";
    private const string Account = "tester:secret";

    // Short enough that its one RTP frame (4 + 12 + 100 bytes) is shown in full by the driver.
    private static readonly byte[] Clip = [.. Enumerable.Range(0, 100).Select(index => (byte)(index * 7 % 251))];

    private static readonly Dictionary<string, byte[]> ServedFiles = new() { ["clip.bin"] = Clip };

    private static readonly string[] UploadOptions = ["--allow-uploads", "--user", Account, "--allow-plaintext-auth"];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Describe_ServedFile_AnswersItsSessionDescription()
    {
        var requests = await RunAgainstSurlAsync("clip.bin", "DESCRIBE");

        AssertAnswered(requests[0], 200);
        Assert.AreEqual(
            "v=0\r\no=- 0 0 IN IP4 127.0.0.1\r\ns=clip.bin\r\nc=IN IP4 0.0.0.0\r\nt=0 0\r\na=control:*\r\n"
            + "a=range:npt=0-\r\nm=application 0 RTP/AVP 96\r\na=rtpmap:96 octet-stream/90000\r\na=control:*\r\n",
            Encoding.ASCII.GetString(requests[0].Body));
        Assert.IsNull(requests[0].SessionId);
    }

    [TestMethod]
    public async Task Setup_InterleavedTcp_AnswersA16HexDigitSession()
    {
        var requests = await RunAgainstSurlAsync("clip.bin", PlayTransport, "SETUP");

        AssertAnswered(requests[0], 200);
        AssertIsSessionId(requests[0].SessionId);
    }

    [TestMethod]
    public async Task PlayThenReceive_ServedFile_DeliversItsBytesThenAnRtcpBye()
    {
        // A libcurl client receives in a loop: each RECEIVE returns with what one read brought, and
        // surl writes the RTCP frame after the last RTP frame, so it may take a second RECEIVE (and
        // a third then waits out the timeout once nothing is left, which is not checked).
        var requests = await RunAgainstSurlAsync("clip.bin", PlayTransport, "SETUP", "PLAY", "RECEIVE", "RECEIVE", "RECEIVE");

        AssertAnswered(requests[1], 200);
        AssertAnswered(requests[2], 0);
        var frames = requests.Skip(1).SelectMany(request => request.Interleaved).ToList();
        var rtp = frames.Where(frame => frame[1] == 0).ToList();
        CollectionAssert.AreEqual(Clip, rtp.SelectMany(frame => frame[16..]).ToArray());
        var ssrc = rtp[0][12..16];
        var rtcp = frames.Last(frame => frame[1] == 1);
        byte[] bye = [0x81, 0xCB, 0x00, 0x01, .. ssrc];
        CollectionAssert.AreEqual(bye, rtcp[^8..]);
    }

    [TestMethod]
    public async Task PauseGetParameterTeardown_AfterPlay_AreEachAnsweredOnTheSameSession()
    {
        var requests = await RunAgainstSurlAsync(
            "clip.bin", PlayTransport, "SETUP", "PLAY", "PAUSE", "GET_PARAMETER", "TEARDOWN");

        var session = requests[0].SessionId;
        AssertIsSessionId(session);
        foreach (var request in requests.Skip(1))
        {
            AssertAnswered(request, 200);
            Assert.AreEqual(session, request.SessionId, request.Request);
        }
    }

    [TestMethod]
    public async Task OptionsThenSetup_AfterTeardown_AreAnsweredAndSetUpTheSameSessionId()
    {
        var requests = await RunAgainstSurlAsync(
            "clip.bin", PlayTransport, "SETUP", "PLAY", "TEARDOWN", "OPTIONS", "SETUP");

        var session = requests[0].SessionId;
        AssertIsSessionId(session);
        AssertAnswered(requests[3], 200);
        AssertAnswered(requests[4], 200);
        Assert.AreEqual(session, requests[4].SessionId);
    }

    [TestMethod]
    public async Task Setup_UdpTransport_Is461WithNoSession()
    {
        var requests = await RunAgainstSurlAsync("clip.bin", "transport:RTP/AVP;unicast;client_port=5000-5001", "SETUP");

        AssertAnswered(requests[0], 461);
        Assert.IsNull(requests[0].SessionId);
    }

    [TestMethod]
    public async Task Play_MadeUpSession_Is454()
    {
        var requests = await RunAgainstSurlAsync("clip.bin", "session-id:DEADBEEF", "PLAY");

        AssertAnswered(requests[0], 454);
    }

    [TestMethod]
    public async Task Record_SessionSetUpToPlayLoggedIn_Is455()
    {
        await using var surl = await StartSurlAsync(UploadOptions);

        var requests = await RunDriverAsync(LoggedInUrlOf(surl, "clip.bin"), surl.UrlOf("clip.bin"), PlayTransport, "SETUP", "RECORD");

        AssertAnswered(requests[1], 455);
    }

    [TestMethod]
    public async Task Record_NoLogin_Is401BeforeItsStateIsJudged()
    {
        var requests = await RunAgainstSurlAsync("clip.bin", PlayTransport, "SETUP", "RECORD");

        AssertAnswered(requests[1], 401);
    }

    [TestMethod]
    public async Task GetParameter_WithABody_Is451()
    {
        var requests = await RunAgainstSurlAsync("clip.bin", PlayTransport, "SETUP", @"body:volume\r\n", "GET_PARAMETER");

        AssertAnswered(requests[1], 451);
    }

    [TestMethod]
    public async Task Announce_UploadsOffLoggedIn_Is403AndStoresNothing()
    {
        await using var surl = await StartSurlAsync(["--user", Account, "--allow-plaintext-auth"]);

        var requests = await RunDriverAsync(LoggedInUrlOf(surl, "rec.bin"), surl.UrlOf("rec.bin"), @"body:v=0\r\n", "ANNOUNCE");

        AssertAnswered(requests[0], 403);
        Assert.IsFalse(File.Exists(Path.Combine(surl.ServedDirectory!, "rec.bin.sdp")));
    }

    [TestMethod]
    public async Task Announce_UploadsOnLoggedIn_StoresTheBodyBesideThePath()
    {
        await using var surl = await StartSurlAsync(UploadOptions);

        var requests = await RunDriverAsync(LoggedInUrlOf(surl, "rec.bin"), surl.UrlOf("rec.bin"), @"body:v=0\r\n", "ANNOUNCE");

        AssertAnswered(requests[0], 200);
        CollectionAssert.AreEqual(
            "v=0\r\n"u8.ToArray(),
            await File.ReadAllBytesAsync(Path.Combine(surl.ServedDirectory!, "rec.bin.sdp"), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task SetupRecordTeardown_UploadsOnLoggedIn_StoresAnEmptyRecording()
    {
        await using var surl = await StartSurlAsync(UploadOptions);

        var requests = await RunDriverAsync(
            LoggedInUrlOf(surl, "rec.bin"), surl.UrlOf("rec.bin"), "no-body", RecordTransport, "SETUP", "RECORD", "TEARDOWN");

        foreach (var request in requests)
        {
            AssertAnswered(request, 200);
        }

        Assert.IsEmpty(await File.ReadAllBytesAsync(Path.Combine(surl.ServedDirectory!, "rec.bin"), TestContext.CancellationToken));
    }

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

    // libcurl sends the URL's user name and password as Basic on every request, which the upload
    // options allow in plain text: a write always needs a login (ADR-0074 decision 7).
    private static string LoggedInUrlOf(SurlOnLoopback surl, string path) =>
        new UriBuilder(surl.UrlOf(path)) { UserName = "tester", Password = "secret" }.Uri.AbsoluteUri;

    private async Task<IReadOnlyList<LibcurlRtspRequestLine>> RunAgainstSurlAsync(
        string path, params string[] steps)
    {
        await using var surl = await StartSurlAsync([]);
        return await RunDriverAsync(surl.UrlOf(path), surl.UrlOf(path), steps);
    }

    private Task<SurlOnLoopback> StartSurlAsync(string[] options) =>
        SurlOnLoopback.StartAsync("rtsp", ServedFiles, [], options, TestContext.CancellationToken);

    private async Task<IReadOnlyList<LibcurlRtspRequestLine>> RunDriverAsync(string url, string streamUri, params string[] steps)
    {
        var lines = await PinnedLibcurlDriver.RunAsync(
            TestContext, PinnedLibcurlDriver.RtspScript, ["--timeout", "3000", url, $"stream-uri:{streamUri}", .. steps]);
        return LibcurlRtspRequestLine.ReadAll(lines);
    }
}
