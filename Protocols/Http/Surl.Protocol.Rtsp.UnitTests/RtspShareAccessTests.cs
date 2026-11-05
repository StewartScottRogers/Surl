using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Rtsp.PatternRandomNumberGenerator;
using static Surl.Protocol.Rtsp.RtspServerHarness;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// The responder hands the content store the account each request's verdict let in, or an
/// anonymous caller, on the scheme it listens on (ADR-0102 decision 2.2): a share the caller may
/// not read is the <c>404</c> a missing presentation gets, and a write in a share it may only read
/// is the <c>403</c> an upload the store does not permit gets (decision 2.3, ADR-0074).
/// </summary>
[TestClass]
public sealed class RtspShareAccessTests
{
    private const string ShareName = "team";

    private const string NextOptions = "OPTIONS * RTSP/1.0\r\nCSeq: 9\r\n\r\n";

    private const string DescribeShared = "DESCRIBE rtsp://127.0.0.1:18554/team/clip.bin RTSP/1.0\r\nCSeq: 1\r\nAccept: application/sdp\r\n\r\n";

    private const string DescribeMissing = "DESCRIBE rtsp://127.0.0.1:18554/missing.bin RTSP/1.0\r\nCSeq: 1\r\nAccept: application/sdp\r\n\r\n";

    private const string AnnounceShared = "ANNOUNCE rtsp://127.0.0.1:18554/team/rec.bin RTSP/1.0\r\nCSeq: 1\r\nContent-Length: 5\r\nContent-Type: application/sdp\r\n\r\nv=0\r\n";

    private const string SetupSharedToPlay = "SETUP rtsp://127.0.0.1:18554/team/clip.bin RTSP/1.0\r\nCSeq: 1\r\nTransport: RTP/AVP/TCP;unicast;interleaved=0-1\r\n\r\n";

    private const string SetupSharedToRecord = "SETUP rtsp://127.0.0.1:18554/team/rec.bin RTSP/1.0\r\nCSeq: 1\r\nTransport: RTP/AVP/TCP;unicast;interleaved=0-1;mode=record\r\n\r\n";

    private const string RecordShared = "RECORD rtsp://127.0.0.1:18554/team/rec.bin RTSP/1.0\r\nCSeq: 2\r\nSession: " + SessionId + "\r\n\r\n";

    private static readonly string SharePath = Path.Join(Path.GetTempPath(), "surl-rtsp-share");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Describe_AccountGrantedReadInTheShare_Is200WithTheSharedPresentation()
    {
        var (written, _) = await ServeShareAsync(DescribeShared, LoggedIn("ann"), Grants(("ann", ContentShareAccess.Read)));

        Assert.StartsWith("RTSP/1.0 200 OK\r\nCSeq: 1\r\n", Latin1(written));
        Assert.Contains("Content-Base: rtsp://127.0.0.1:18554/team/clip.bin\r\n", Latin1(written));
        Assert.Contains("s=clip.bin\r\n", Latin1(written));
    }

    [TestMethod]
    public async Task Announce_AccountGrantedReadInTheShare_Is403AndStoresNothing()
    {
        var (written, shareFileSystem) = await ServeShareAsync(AnnounceShared + NextOptions, LoggedIn("ann"), Grants(("ann", ContentShareAccess.Read)));

        var heads = RtspOutput.Heads(written);
        Assert.AreEqual(ResponseHead("403 Forbidden", "1"), heads[0]);
        Assert.StartsWith("RTSP/1.0 200 OK\r\nCSeq: 9\r\n", heads[1]);
        CollectionAssert.AreEquivalent(new[] { "clip.bin" }, shareFileSystem.EnumerateDirectoryEntryNames(SharePath).ToArray());
    }

    [TestMethod]
    public async Task Announce_AccountGrantedReadWriteInTheShare_Is200AndStoresTheDescription()
    {
        var (written, shareFileSystem) = await ServeShareAsync(AnnounceShared, LoggedIn("ann"), Grants(("ann", ContentShareAccess.ReadWrite)));

        Assert.AreEqual(ResponseHead("200 OK", "1"), RtspOutput.Heads(written)[0]);
        CollectionAssert.AreEquivalent(new[] { "clip.bin", "rec.bin.sdp" }, shareFileSystem.EnumerateDirectoryEntryNames(SharePath).ToArray());
    }

    [TestMethod]
    public async Task Record_AccountGrantedReadInTheShare_Is403AfterTheSetup()
    {
        var (written, shareFileSystem) = await ServeShareAsync(SetupSharedToRecord + RecordShared, LoggedIn("ann"), Grants(("ann", ContentShareAccess.Read)));

        var heads = RtspOutput.Heads(written);
        Assert.StartsWith("RTSP/1.0 200 OK\r\nCSeq: 1\r\n", heads[0]);
        Assert.StartsWith("RTSP/1.0 403 Forbidden\r\nCSeq: 2\r\n", heads[1]);
        CollectionAssert.AreEquivalent(new[] { "clip.bin" }, shareFileSystem.EnumerateDirectoryEntryNames(SharePath).ToArray());
    }

    [TestMethod]
    public async Task Describe_AccountGrantedNothingInTheShare_IsThe404AMissingPresentationGets()
    {
        var policy = LoggedIn("bob");
        var grants = Grants(("ann", ContentShareAccess.ReadWrite));

        var (refused, _) = await ServeShareAsync(DescribeShared, policy, grants);
        var (missing, _) = await ServeShareAsync(DescribeMissing, policy, grants);

        Assert.AreEqual(ResponseHead("404 Not Found", "1"), Latin1(refused));
        Assert.AreEqual(Latin1(missing), Latin1(refused));
    }

    [TestMethod]
    public async Task SetupToPlay_AccountGrantedNothingInTheShare_Is404()
    {
        var (written, _) = await ServeShareAsync(SetupSharedToPlay, LoggedIn("bob"), Grants(("ann", ContentShareAccess.ReadWrite)));

        Assert.AreEqual(ResponseHead("404 Not Found", "1"), Latin1(written));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public async Task Describe_VerdictNamingNoAccountInAClosedShare_IsAnsweredAsAnAnonymousCallerWith404(string? accountName)
    {
        var (written, _) = await ServeShareAsync(DescribeShared, LoggedIn(accountName), Grants(("ann", ContentShareAccess.ReadWrite)));

        Assert.AreEqual(ResponseHead("404 Not Found", "1"), Latin1(written));
    }

    [TestMethod]
    public async Task Describe_AnonymousCallerInAShareGrantingEveryone_Is200()
    {
        var (written, _) = await ServeShareAsync(DescribeShared, LoggedIn(null), Grants((ContentShare.EveryoneMember, ContentShareAccess.Read)));

        Assert.StartsWith("RTSP/1.0 200 OK\r\nCSeq: 1\r\n", Latin1(written));
    }

    [TestMethod]
    public async Task Describe_ShareWithNoAccessRows_Is200AsBeforeShareAccess()
    {
        var (written, _) = await ServeShareAsync(DescribeShared, LoggedIn(null), grants: null);

        Assert.StartsWith("RTSP/1.0 200 OK\r\nCSeq: 1\r\n", Latin1(written));
    }

    [TestMethod]
    [DataRow("rtsp", "200 OK")]
    [DataRow("ftp", "404 Not Found")]
    public async Task Describe_ShareLimitedToAScheme_IsServedOnlyOnThatScheme(string scheme, string statusLine)
    {
        var (written, _) = await ServeShareAsync(DescribeShared, LoggedIn(null), grants: null, schemes: [scheme]);

        Assert.StartsWith($"RTSP/1.0 {statusLine}\r\nCSeq: 1\r\n", Latin1(written));
    }

    private static UnitTestAuthenticationPolicy LoggedIn(string? accountName) =>
        new(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], accountName));

    private static Dictionary<string, ContentShareAccess> Grants(params (string Member, ContentShareAccess Access)[] grants) =>
        grants.ToDictionary(grant => grant.Member, grant => grant.Access, StringComparer.Ordinal);

    // Serves the request over a root holding only the standard files and the share "team", whose
    // one file is clip.bin, with uploads allowed; returns what was written and the share's files.
    private async Task<(byte[] Written, InMemoryContentFileSystem ShareFileSystem)> ServeShareAsync(string request, IAuthenticationPolicy policy, Dictionary<string, ContentShareAccess>? grants, string[]? schemes = null)
    {
        var shareFileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        shareFileSystem.CreateDirectory(SharePath);
        using (var clip = shareFileSystem.CreateFileForAsyncWrite(Path.Join(SharePath, "clip.bin")))
        {
            clip.Write(Ascii(ClipBody));
        }

        var share = new ContentShare(ShareName, SharePath, shareFileSystem)
        {
            Grants = grants,
            Schemes = schemes is null ? null : new HashSet<string>(schemes, StringComparer.Ordinal),
        };
        var contentStore = new ContentStore(Root, StandardFileSystem(), new ContentExposureOptions { AllowUploads = true }, [share], []);
        var server = new RtspProtocolServer(contentStore, policy, new PatternRandomNumberGenerator());
        var connection = new InMemoryConnection([Ascii(request)], peerHalfClosesWhenExhausted: true);

        await server.ServeAsync(connection, Context(new RecordingExchangeLog(), new ManualTimeProvider(Now), TestContext.CancellationToken));

        return (connection.WrittenBytes, shareFileSystem);
    }
}
