using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Gopher.GopherTestExchange;

namespace Surl.Protocol.Gopher;

/// <summary>
/// Gopher has no login, so the server hands the content store an anonymous caller on the scheme
/// it listens on (ADR-0102 decision 2.2): a share closed to anonymous callers, or limited to
/// other schemes, gets the error menu a missing selector gets (ADR-0102 decision 2.3).
/// </summary>
[TestClass]
public sealed class GopherShareAccessTests
{
    private const string SharedText = "shared\n";

    private static readonly string SharePath = Path.Join(Path.GetTempPath(), "surl-gopher-share-tests");
    private static readonly DateTimeOffset FileTime = new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Selector_ShareOpenToEveryone_SendsTheSharedFile()
    {
        var written = await ServeSelectorAsync(ShareStore(Grants(ContentShare.EveryoneMember)), "/team/notes.txt");

        Assert.AreEqual(SharedText, Utf8(written));
    }

    [TestMethod]
    public async Task Selector_ShareWithNoAccessRows_IsServedAsBeforeShareAccess()
    {
        var written = await ServeSelectorAsync(ShareStore(grants: null), "/team/notes.txt");

        Assert.AreEqual(SharedText, Utf8(written));
    }

    [TestMethod]
    [DataRow("/team/notes.txt", DisplayName = "A file in the share")]
    [DataRow("/team", DisplayName = "The share itself")]
    public async Task Selector_ShareClosedToAnonymous_IsTheReplyAMissingSelectorGets(string selector)
    {
        var store = ShareStore(Grants("ann"));

        var written = await ServeSelectorAsync(store, selector);

        CollectionAssert.AreEqual(await ServeSelectorAsync(store, "/missing.txt"), written);
        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("missing-selector", "stdout.bin"), written);
    }

    [TestMethod]
    [DataRow("gopher", "gopher", true)]
    [DataRow("gophers", "gophers", true)]
    [DataRow("gophers", "gopher", false)]
    [DataRow("http", "gopher", false)]
    public async Task Selector_ShareLimitedToAScheme_IsServedOnlyOverThatScheme(string shareScheme, string listeningScheme, bool isServed)
    {
        var store = ShareStore(Grants(ContentShare.EveryoneMember), schemes: [shareScheme]);

        var written = await ServeSelectorAsync(store, "/team/notes.txt", listeningScheme);

        CollectionAssert.AreEqual(isServed ? Encoding.ASCII.GetBytes(SharedText) : RecordedFixture.ReadBytes("missing-selector", "stdout.bin"), written);
    }

    private async Task<byte[]> ServeSelectorAsync(ContentStore store, string selector, string scheme = "gopher")
    {
        var connection = new InMemoryConnection(RecordedFixture.Whole(Ascii(selector + "\r\n")));
        var context = new ExchangeContext(
            1,
            new ListenUrl(scheme, "127.0.0.1", 18634).WithBoundPort(18634),
            new IPEndPoint(IPAddress.Loopback, 18634),
            new IPEndPoint(IPAddress.Loopback, 50000),
            new RecordingExchangeLog(),
            TimeProvider.System,
            TestContext.CancellationToken);

        await new GopherProtocolServer(store).ServeAsync(connection, context);

        return connection.WrittenBytes;
    }

    private static Dictionary<string, ContentShareAccess> Grants(string member) =>
        new(StringComparer.Ordinal) { [member] = ContentShareAccess.Read };

    private static ContentStore ShareStore(Dictionary<string, ContentShareAccess>? grants, string[]? schemes = null)
    {
        var shareFileSystem = new UnitTestInMemoryContentFileSystem()
            .AddDirectory(SharePath)
            .AddFile(Path.Join(SharePath, "notes.txt"), Encoding.ASCII.GetBytes(SharedText), FileTime);
        var share = new ContentShare("team", SharePath, shareFileSystem)
        {
            Grants = grants,
            Schemes = schemes is null ? null : new HashSet<string>(schemes, StringComparer.Ordinal),
        };

        return new ContentStore(Root, FileSystemWithDotFiles(), new ContentExposureOptions { ListDirectories = true }, [share], []);
    }
}
