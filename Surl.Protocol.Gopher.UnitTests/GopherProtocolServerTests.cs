using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Gopher;

[TestClass]
public sealed class GopherProtocolServerTests
{
    private const string FileBody = "Hello from Surl.\n";
    private const string NothingServedHere = "3Nothing is served at this selector.\t\terror.host\t1\r\n.\r\n";

    private static readonly string Root = Path.Join(Path.GetTempPath(), "surl-gopher-tests");
    private static readonly DateTimeOffset FileTime = new(2026, 9, 1, 8, 30, 0, TimeSpan.Zero);

    // The engine hands a gophers exchange a connection whose implicit handshake is done (ADR-0010).
    private static readonly ListenUrl GophersListenUrl = new ListenUrl("gophers", "127.0.0.1", 18634).WithBoundPort(18634);
    private static readonly TlsSession ImplicitTlsSession = new(SslProtocols.Tls12, TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384, null, null, null);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullContentStore_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new GopherProtocolServer(null!));
    }

    [TestMethod]
    public void Schemes_AreGopherThenGophers()
    {
        var server = new GopherProtocolServer(new ContentStore(Root, StandardFileSystem()));

        CollectionAssert.AreEqual(new[] { "gopher", "gophers" }, server.Schemes.ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ServeAsync_RecordedGophersFileSelectorOverTls_SendsTheFileBytesCurlAcceptedAndCloses(bool oneBytePerRead)
    {
        var (connection, log) = await ServeAsync(
            RecordedFixture.ReadRequestBytes("gophers-file-selector"),
            oneBytePerRead: oneBytePerRead,
            listenUrl: GophersListenUrl,
            tlsSession: ImplicitTlsSession);

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("gophers-file-selector", "stdout.bin"), connection.WrittenBytes);
        Assert.AreEqual(FileBody, Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreSame(ImplicitTlsSession, connection.TlsSession);
        Assert.AreEqual($"Selector \"/file.txt\": 17 bytes of {Path.Join(Root, "file.txt")}", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_RecordedGophersRootSelectorOverTls_SendsTheMenuCurlAccepted()
    {
        var (connection, log) = await ServeAsync(
            RecordedFixture.ReadRequestBytes("gophers-root-menu"),
            listenUrl: GophersListenUrl,
            tlsSession: ImplicitTlsSession);

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("gophers-root-menu", "stdout.bin"), connection.WrittenBytes);
        Assert.AreEqual(
            "0file.txt\t/file.txt\t127.0.0.1\t18634\r\n1sub\t/sub\t127.0.0.1\t18634\r\n.\r\n",
            Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreSame(ImplicitTlsSession, connection.TlsSession);
        Assert.AreEqual($"Selector \"\": a menu of the 2 entries of {Root}", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        var server = new GopherProtocolServer(new ContentStore(Root, StandardFileSystem()));
        var connection = new InMemoryConnection([]);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context(new RecordingExchangeLog())));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(connection, null!));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ServeAsync_RecordedFileSelector_SendsTheFileBytesCurlAcceptedAndCloses(bool oneBytePerRead)
    {
        var (connection, log) = await ServeAsync(RecordedFixture.ReadRequestBytes("file-selector"), oneBytePerRead: oneBytePerRead);

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("file-selector", "stdout.bin"), connection.WrittenBytes);
        Assert.AreEqual(FileBody, Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual($"Selector \"/file.txt\": 17 bytes of {Path.Join(Root, "file.txt")}", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_RecordedRootSelector_SendsTheMenuCurlAccepted()
    {
        var (connection, log) = await ServeAsync(RecordedFixture.ReadRequestBytes("root-menu"));

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("root-menu", "stdout.bin"), connection.WrittenBytes);
        Assert.AreEqual(
            "0file.txt\t/file.txt\t127.0.0.1\t18634\r\n1sub\t/sub\t127.0.0.1\t18634\r\n.\r\n",
            Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual($"Selector \"\": a menu of the 2 entries of {Root}", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_RecordedMissingSelector_SendsTheErrorMenuCurlAccepted()
    {
        var (connection, log) = await ServeAsync(RecordedFixture.ReadRequestBytes("missing-selector"));

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("missing-selector", "stdout.bin"), connection.WrittenBytes);
        Assert.AreEqual(NothingServedHere, Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual($"Selector \"/missing.txt\": error menu, nothing exists at {Path.Join(Root, "missing.txt")}", log.Notes.Single());
    }

    [TestMethod]
    [DataRow("path-as-is-dot-segment")]
    [DataRow("encoded-dot-segment")]
    public async Task ServeAsync_RecordedDotSegmentSelector_IsRefusedByTheContentStoreWithTheErrorMenuCurlAccepted(string caseName)
    {
        var (connection, log) = await ServeAsync(RecordedFixture.ReadRequestBytes(caseName));

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes(caseName, "stdout.bin"), connection.WrittenBytes);
        Assert.AreEqual(NothingServedHere, Utf8(connection.WrittenBytes));
        Assert.EndsWith("error menu, refused by the content store (DotSegment)", log.Notes.Single());
    }

    [TestMethod]
    [DataRow("/../file.txt\r\n")]
    [DataRow("/%2e%2e/file.txt\r\n")]
    [DataRow("/%2E%2E/file.txt\r\n")]
    [DataRow("/.%2e/file.txt\r\n")]
    [DataRow("..\r\n")]
    [DataRow("/sub/../file.txt\r\n")]
    [DataRow("/sub/%2e%2e\r\n")]
    public async Task ServeAsync_DotDotSelector_IsRefusedByTheContentStore(string request)
    {
        var (connection, log) = await ServeAsync(Encoding.ASCII.GetBytes(request));

        Assert.AreEqual(NothingServedHere, Utf8(connection.WrittenBytes));
        Assert.EndsWith("(DotSegment)", log.Notes.Single());
    }

    [TestMethod]
    [DataRow("/file.txt\n")]
    [DataRow("file.txt\r\n")]
    [DataRow("/file.txt\r\nignored after the line")]
    public async Task ServeAsync_SelectorLineVariants_ServeTheFile(string request)
    {
        var (connection, _) = await ServeAsync(Encoding.ASCII.GetBytes(request));

        Assert.AreEqual(FileBody, Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("/file.txt\thello world\r\n")]
    [DataRow("/file.txt\t+\r\n")]
    public async Task ServeAsync_TextAfterATab_IsIgnoredAndTheSelectorServed(string request)
    {
        var (connection, log) = await ServeAsync(Encoding.ASCII.GetBytes(request));

        Assert.AreEqual(FileBody, Utf8(connection.WrittenBytes));
        Assert.AreEqual("Selector \"/file.txt\": the text after its TAB was ignored.", log.Notes[0]);
    }

    [TestMethod]
    public async Task ServeAsync_NonAsciiSelector_IsPercentEncodedAndMapsToTheFile()
    {
        var fileSystem = StandardFileSystem().AddFile(Path.Join(Root, "café.txt"), "c"u8.ToArray(), FileTime);

        var (connection, log) = await ServeAsync([.. "/caf"u8, 0xC3, 0xA9, .. ".txt\r\n"u8], fileSystem);

        Assert.AreEqual("c", Utf8(connection.WrittenBytes));
        Assert.StartsWith(@"Selector ""/caf\xC3\xA9.txt""", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_ControlCharacterInSelector_IsRefusedAndRenderedSafelyInTheLog()
    {
        var (connection, log) = await ServeAsync([.. "/a"u8, 0x1B, .. "\r\n"u8]);

        Assert.AreEqual(NothingServedHere, Utf8(connection.WrittenBytes));
        Assert.AreEqual(@"Selector ""/a\x1B"": error menu, refused by the content store (ControlCharacter)", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_SubdirectorySelector_ListsItsEntriesWithSelectorsUnderIt()
    {
        var fileSystem = StandardFileSystem()
            .AddFile(Path.Join(Root, "sub", "a b%.txt"), [], FileTime)
            .AddFile(Path.Join(Root, "sub", "picture.PNG"), [], FileTime)
            .AddFile(Path.Join(Root, "sub", "anim.gif"), [], FileTime)
            .AddFile(Path.Join(Root, "sub", "page.html"), [], FileTime)
            .AddFile(Path.Join(Root, "sub", "data.bin"), [], FileTime)
            .AddFile(Path.Join(Root, "sub", "naïve"), [], FileTime)
            .AddDirectory(Path.Join(Root, "sub", "inner"));

        var (connection, _) = await ServeAsync("/sub/\r\n"u8.ToArray(), fileSystem);

        Assert.AreEqual(
            "0a b%.txt\t/sub/a%20b%25.txt\t127.0.0.1\t18634\r\n"
            + "ganim.gif\t/sub/anim.gif\t127.0.0.1\t18634\r\n"
            + "9data.bin\t/sub/data.bin\t127.0.0.1\t18634\r\n"
            + "1inner\t/sub/inner\t127.0.0.1\t18634\r\n"
            + "9naïve\t/sub/na%C3%AFve\t127.0.0.1\t18634\r\n"
            + "hpage.html\t/sub/page.html\t127.0.0.1\t18634\r\n"
            + "Ipicture.PNG\t/sub/picture.PNG\t127.0.0.1\t18634\r\n"
            + ".\r\n",
            Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("/sub/a%20b%25.txt\r\n")]
    [DataRow("/sub/a b%25.txt\r\n")]
    public async Task ServeAsync_SelectorAMenuOffered_MapsBackToItsEntry(string request)
    {
        var fileSystem = StandardFileSystem().AddFile(Path.Join(Root, "sub", "a b%.txt"), "x"u8.ToArray(), FileTime);

        var (connection, _) = await ServeAsync(Encoding.ASCII.GetBytes(request), fileSystem);

        Assert.AreEqual("x", Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_EmptyDirectory_SendsOnlyTheEndLine()
    {
        var (connection, _) = await ServeAsync("/sub\r\n"u8.ToArray());

        Assert.AreEqual(".\r\n", Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("0.0.0.0", "192.0.2.7", "192.0.2.7")]
    [DataRow("::", "::ffff:192.0.2.7", "192.0.2.7")]
    [DataRow("::", "2001:db8::7", "2001:db8::7")]
    [DataRow("::", "fe80::7%12", "fe80::7")]
    [DataRow("localhost", "127.0.0.1", "localhost")]
    [DataRow("::1", "::1", "::1")]
    public async Task ServeAsync_Menu_NamesTheListenHostOrForAWildcardTheLocalAddress(string listenHost, string localAddress, string expectedHost)
    {
        var listenUrl = new ListenUrl("gopher", listenHost, 70).WithBoundPort(70);
        var local = new IPEndPoint(IPAddress.Parse(localAddress), 70);

        var (connection, _) = await ServeAsync("\r\n"u8.ToArray(), listenUrl: listenUrl, localEndPoint: local);

        Assert.StartsWith($"0file.txt\t/file.txt\t{expectedHost}\t70\r\n", Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_Menu_WildcardWithNoIpLocalEndPoint_NamesTheListenHost()
    {
        var (connection, _) = await ServeAsync(
            "\r\n"u8.ToArray(),
            listenUrl: new ListenUrl("gopher", "0.0.0.0", 7070),
            localEndPoint: new DnsEndPoint("example.test", 7070));

        Assert.StartsWith("0file.txt\t/file.txt\t0.0.0.0\t7070\r\n", Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_FileThatShrinksWhileSent_AbortsTheConnection()
    {
        var fileSystem = StandardFileSystem().AddFile(Path.Join(Root, "file.txt"), "short"u8.ToArray(), FileTime, reportedLength: 50);

        var (connection, log) = await ServeAsync("/file.txt\r\n"u8.ToArray(), fileSystem);

        Assert.AreEqual("short", Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.Aborted);
        Assert.AreEqual($"{Path.Join(Root, "file.txt")} shrank to 5 bytes while it was sent; the connection was aborted.", log.Notes[1]);
    }

    [TestMethod]
    [DataRow("/file.txt\r\n", "vanished before it was read")]
    [DataRow("/sub\r\n", "was no longer a directory when it was listed")]
    public async Task ServeAsync_EntryThatVanishesAfterMapping_SendsTheErrorMenu(string request, string note)
    {
        var (connection, log) = await ServeAsync(Encoding.ASCII.GetBytes(request), new VanishingContentFileSystem(StandardFileSystem()));

        Assert.AreEqual(NothingServedHere, Utf8(connection.WrittenBytes));
        Assert.EndsWith(note, log.Notes.Single());
    }

    [TestMethod]
    [DataRow(new byte[0], "ConnectionClosed")]
    [DataRow(new byte[] { (byte)'/', (byte)'f' }, "ConnectionClosed")]
    public async Task ServeAsync_ClientClosesBeforeALine_SendsNothing(byte[] request, string outcome)
    {
        var (connection, log) = await ServeAsync(request);

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual($"No selector was read ({outcome}); the connection was closed with no reply.", log.Notes.Single());
    }

    [TestMethod]
    [DataRow(10, false)]
    [DataRow(11, true)]
    [DataRow(0, true)]
    public async Task ServeAsync_LineLimit_CountsTheLineEndingAndClosesWithNothingPastIt(long maxLineBytes, bool served)
    {
        var limits = ExchangeLimits.Default with { MaxLineBytes = maxLineBytes };

        var (connection, log) = await ServeAsync("/file.txt\r\n"u8.ToArray(), limits: limits);

        Assert.AreEqual(served ? FileBody : string.Empty, Utf8(connection.WrittenBytes));
        Assert.AreEqual(served, !log.Notes[0].Contains("LineTooLong", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ServeAsync_LongLineInOneByteReads_StopsReadingAtTheLimit()
    {
        var limits = ExchangeLimits.Default with { MaxLineBytes = 4 };

        var (connection, log) = await ServeAsync(Encoding.ASCII.GetBytes(new string('a', 5000) + "\r\n"), limits: limits, oneBytePerRead: true);

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual("No selector was read (LineTooLong); the connection was closed with no reply.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_HeadTimeout_ClosesWithNothing()
    {
        var limits = ExchangeLimits.Default with { HeadTimeout = TimeSpan.Zero };

        var (connection, log) = await ServeAsync([], limits: limits, peerHalfCloses: false);

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual("No selector was read (HeadTimedOut); the connection was closed with no reply.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelled_Throws()
    {
        var server = new GopherProtocolServer(new ContentStore(Root, StandardFileSystem()));
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => server.ServeAsync(connection, Context(new RecordingExchangeLog(), cancellationToken: new CancellationToken(true))));
    }

    private static string Utf8(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static InMemoryContentFileSystem StandardFileSystem() => new InMemoryContentFileSystem()
        .AddDirectory(Root)
        .AddDirectory(Path.Join(Root, "sub"))
        .AddFile(Path.Join(Root, "file.txt"), Encoding.ASCII.GetBytes(FileBody), FileTime);

    private static ExchangeContext Context(
        IExchangeLog log,
        ListenUrl? listenUrl = null,
        EndPoint? localEndPoint = null,
        ExchangeLimits? limits = null,
        CancellationToken cancellationToken = default) => new(
        1,
        listenUrl ?? new ListenUrl("gopher", "127.0.0.1", 18634).WithBoundPort(18634),
        localEndPoint ?? new IPEndPoint(IPAddress.Loopback, 18634),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        new FixedTimeProvider(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero)),
        cancellationToken)
        {
            Limits = limits ?? ExchangeLimits.Default,
        };

    private async Task<(InMemoryConnection Connection, RecordingExchangeLog Log)> ServeAsync(
        byte[] request,
        IContentFileSystem? fileSystem = null,
        bool oneBytePerRead = false,
        bool peerHalfCloses = true,
        ListenUrl? listenUrl = null,
        EndPoint? localEndPoint = null,
        ExchangeLimits? limits = null,
        TlsSession? tlsSession = null)
    {
        var server = new GopherProtocolServer(new ContentStore(Root, fileSystem ?? StandardFileSystem()));
        var chunks = oneBytePerRead ? RecordedFixture.OneBytePerRead(request) : RecordedFixture.Whole(request);
        var connection = new InMemoryConnection(chunks, peerHalfClosesWhenExhausted: peerHalfCloses, initialTlsSession: tlsSession);
        var log = new RecordingExchangeLog();

        await server.ServeAsync(connection, Context(log, listenUrl, localEndPoint, limits, TestContext.CancellationToken));

        return (connection, log);
    }
}
