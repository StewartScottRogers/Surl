using System.Text;
using Surl.Content;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// The answerer hands the content store the account the authentication session logged in, or
/// an anonymous caller, on the scheme it listens on (ADR-0102 decision 2.2): a share the caller
/// may not read is the 404 a missing path gets, and a write is the 405 it always is (decision 2.3).
/// </summary>
public sealed partial class HttpRequestAnswererTests
{
    private const string ShareName = "team";

    private static readonly string SharePath = Path.Join(Path.GetTempPath(), "surl-http-share");

    [TestMethod]
    public async Task AnswerAsync_AccountGrantedReadGetsAFileInTheShare_IsA200WithTheSharedFile()
    {
        var answer = await ShareAnswerer(LoggedIn("ann"), Grants(("ann", ContentShareAccess.Read))).AnswerAsync(Request("GET", "/team/notes.txt"), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.Ok, answer.Status);
        Assert.AreEqual(Path.Join(SharePath, "notes.txt"), ((HttpAnswerBody.ContentFile)answer.Body).Mapping.Location);
    }

    [TestMethod]
    public async Task AnswerAsync_AccountGrantedReadPutsAFileInTheShare_IsA405AllowingGetAndHead()
    {
        var answer = await ShareAnswerer(LoggedIn("ann"), Grants(("ann", ContentShareAccess.Read))).AnswerAsync(Request("PUT", "/team/notes.txt"), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.MethodNotAllowed, answer.Status);
        Assert.AreEqual("GET, HEAD", answer.Fields.Single(field => field.Key == "Allow").Value);
        Assert.IsTrue(answer.IsRefusal);
    }

    [TestMethod]
    public async Task AnswerAsync_AccountGrantedNothingGetsAFileInTheShare_IsTheEmpty404AMissingPathGets()
    {
        var answerer = ShareAnswerer(LoggedIn("bob"), Grants(("ann", ContentShareAccess.ReadWrite)));

        var refused = await answerer.AnswerAsync(Request("GET", "/team/notes.txt"), TestContext.CancellationToken);
        var missing = await answerer.AnswerAsync(Request("GET", "/missing.txt"), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.NotFound, refused.Status);
        CollectionAssert.AreEqual(missing.Fields.ToArray(), refused.Fields.ToArray());
        Assert.AreSame(HttpAnswerBody.None, refused.Body);
        Assert.AreEqual(missing.IsRefusal, refused.IsRefusal);
    }

    [TestMethod]
    public async Task AnswerAsync_AnonymousLoginGetsAFileInAClosedShare_IsA404()
    {
        var answer = await ShareAnswerer(Anonymous(), Grants(("ann", ContentShareAccess.ReadWrite))).AnswerAsync(Request("HEAD", "/team/notes.txt"), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.NotFound, answer.Status);
    }

    [TestMethod]
    public async Task AnswerAsync_AnonymousLoginGetsAFileInAShareGrantingEveryone_IsA200()
    {
        var answer = await ShareAnswerer(Anonymous(), Grants((ContentShare.EveryoneMember, ContentShareAccess.Read))).AnswerAsync(Request("GET", "/team/notes.txt"), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.Ok, answer.Status);
    }

    [TestMethod]
    public async Task AnswerAsync_AnonymousLoginGetsAFileInAShareWithNoAccessRows_IsA200AsBeforeShareAccess()
    {
        var answer = await ShareAnswerer(Anonymous(), grants: null).AnswerAsync(Request("GET", "/team/notes.txt"), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.Ok, answer.Status);
    }

    [TestMethod]
    public async Task AnswerAsync_VerdictWithAnEmptyAccountName_IsAnsweredAsAnAnonymousCaller()
    {
        var answer = await ShareAnswerer(LoggedIn(string.Empty), Grants(("ann", ContentShareAccess.Read))).AnswerAsync(Request("GET", "/team/notes.txt"), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.NotFound, answer.Status);
    }

    [TestMethod]
    public async Task AnswerAsync_ShareLimitedToTheListeningScheme_IsServed()
    {
        var answer = await ShareAnswerer(Anonymous(), grants: null, schemes: ["HTTP"]).AnswerAsync(Request("GET", "/team/notes.txt"), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.Ok, answer.Status);
    }

    [TestMethod]
    public async Task AnswerAsync_ShareLimitedToAnotherScheme_IsA404()
    {
        var answer = await ShareAnswerer(Anonymous(), grants: null, schemes: ["ftp"]).AnswerAsync(Request("GET", "/team/notes.txt"), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.NotFound, answer.Status);
    }

    [TestMethod]
    public async Task AnswerAsync_LoginBoundToTheBody_IsCheckedForTheAccountTheBodyCheckLoggedIn()
    {
        var bodyCheck = new UnitTestRequestBodyCheck(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "ann"));
        var policy = new UnitTestAuthenticationPolicy(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null, BodyCheck: bodyCheck));

        var answer = await ShareAnswerer(policy, Grants(("ann", ContentShareAccess.Read))).AnswerAsync(Request("GET", "/team/notes.txt"), TestContext.CancellationToken);

        Assert.AreEqual(HttpStatus.Ok, answer.Status);
        Assert.HasCount(1, bodyCheck.JudgedBodySha256s);
    }

    private static UnitTestAuthenticationPolicy LoggedIn(string accountName) =>
        new(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], accountName));

    // A login --allow-anonymous accepted unchecked carries no account name (ADR-0049 section 6).
    private static UnitTestAuthenticationPolicy Anonymous() =>
        new(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null));

    private static Dictionary<string, ContentShareAccess> Grants(params (string Member, ContentShareAccess Access)[] grants) =>
        grants.ToDictionary(grant => grant.Member, grant => grant.Access, StringComparer.Ordinal);

    private HttpRequestAnswerer ShareAnswerer(IAuthenticationPolicy policy, Dictionary<string, ContentShareAccess>? grants, string[]? schemes = null)
    {
        var shareFileSystem = new UnitTestInMemoryContentFileSystem()
            .AddDirectory(SharePath)
            .AddFile(Path.Join(SharePath, "notes.txt"), Encoding.ASCII.GetBytes("shared"), FileTime);
        var share = new ContentShare(ShareName, SharePath, shareFileSystem)
        {
            Grants = grants,
            Schemes = schemes is null ? null : new HashSet<string>(schemes, StringComparer.Ordinal),
        };
        var contentStore = new ContentStore(Root, StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot, [share], []);

        return new HttpRequestAnswerer(Context(new RecordingExchangeLog(), new FixedTimeProvider(Now), TestContext.CancellationToken), contentStore, policy.StartHttpConnection(null));
    }
}
