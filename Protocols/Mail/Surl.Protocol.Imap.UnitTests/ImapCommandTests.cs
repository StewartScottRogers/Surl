using Surl.Protocol.Abstractions;
using static Surl.Protocol.Imap.ImapTestExchange;

namespace Surl.Protocol.Imap;

/// <summary>
/// ADR-0055 decisions 2 and 3: the greeting, the capabilities per state, tags, and the commands
/// any state answers.
/// </summary>
[TestClass]
public sealed class ImapCommandTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var clock = new ManualTimeProvider();
        var policy = new AnonymousAuthenticationPolicy();
        var store = AnonymousStore(clock);

        Assert.ThrowsExactly<ArgumentNullException>(() => new ImapProtocolServer(null!, policy, store));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ImapProtocolServer(policy, null!, store));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ImapProtocolServer(policy, policy, null!));
    }

    [TestMethod]
    public async Task ServeAsync_NullArgument_Throws()
    {
        var clock = new ManualTimeProvider();
        var server = Server(AnonymousStore(clock));
        var connection = new InMemoryConnection([]);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context(clock, TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(connection, null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await server.WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, TestContext.CancellationToken));
    }

    [TestMethod]
    public void Schemes_IsImapAlone()
    {
        CollectionAssert.AreEqual(new[] { "imap" }, Server(AnonymousStore(new ManualTimeProvider())).Schemes.ToList());
    }

    [TestMethod]
    [DataRow(ConnectionRefusal.TooManyConnections)]
    [DataRow(ConnectionRefusal.TooManyConnectionsFromAddress)]
    public async Task WriteRefusalAsync_EitherRefusal_WritesByeAndCompletesWrites(ConnectionRefusal refusal)
    {
        var connection = new InMemoryConnection([]);

        await Server(AnonymousStore(new ManualTimeProvider())).WriteRefusalAsync(connection, refusal, TestContext.CancellationToken);

        Assert.AreEqual("* BYE surl Too many connections, closing\r\n", Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_PeerClosesAtOnce_WritesOnlyTheGreeting()
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(Server(AnonymousStore(clock)), string.Empty, clock, TestContext.CancellationToken);

        Assert.AreEqual(Greeting, Utf8(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_Capability_ListsTheCapabilitiesBeforeAndAfterALogin()
    {
        var responses = await ResponsesAsync("a CAPABILITY\r\nb LOGIN u p\r\nc capability\r\n", TestContext.CancellationToken);

        Assert.AreEqual(
            $"* CAPABILITY {Capabilities}\r\na OK CAPABILITY completed\r\nb OK LOGIN completed\r\n"
            + "* CAPABILITY IMAP4rev1 UIDPLUS UNSELECT NAMESPACE CHILDREN ID MOVE APPENDLIMIT=104857600\r\nc OK CAPABILITY completed\r\n",
            responses);
    }

    [TestMethod]
    public async Task ServeAsync_NoMaxFilesizeAndNoClearPasswordLogin_LeavesOutAppendLimitAndSaysLoginDisabled()
    {
        var clock = new ManualTimeProvider();
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 0 };
        var server = Server(AnonymousStore(clock), new ScriptedLoginPolicy(isClearPasswordLoginOffered: false));

        var connection = await ServeAsync(server, "a CAPABILITY\r\n", clock, TestContext.CancellationToken, limits);

        const string capabilities = "IMAP4rev1 SASL-IR UIDPLUS UNSELECT NAMESPACE CHILDREN ID MOVE LOGINDISABLED";
        Assert.AreEqual($"* OK [CAPABILITY {capabilities}] surl ready\r\n* CAPABILITY {capabilities}\r\na OK CAPABILITY completed\r\n", Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("a CAPABILITY x\r\n", "a BAD Invalid arguments\r\n")]
    [DataRow("a NOOP\r\n", "a OK NOOP completed\r\n")]
    [DataRow("a NOOP x\r\n", "a BAD Invalid arguments\r\n")]
    [DataRow("a LOGOUT x\r\n", "a BAD Invalid arguments\r\n")]
    [DataRow("a ID (\"name\" \"curl\")\r\n", "* ID NIL\r\na OK ID completed\r\n")]
    [DataRow("a ID NIL\r\n", "* ID NIL\r\na OK ID completed\r\n")]
    [DataRow("a NAMESPACE\r\n", "* NAMESPACE ((\"\" \"/\")) NIL NIL\r\na OK NAMESPACE completed\r\n")]
    [DataRow("a NAMESPACE x\r\n", "a BAD Invalid arguments\r\n")]
    [DataRow("a XYZZY\r\n", "a BAD Command not recognized\r\n")]
    [DataRow("a STORE 1 FLAGS (\\Seen)\r\n", "a BAD No mailbox selected\r\n")]
    [DataRow("a\r\n", "a BAD Command not recognized\r\n")]
    [DataRow("a (\r\n", "a BAD Command not recognized\r\n")]
    [DataRow("\r\n", "* BAD Invalid tag\r\n")]
    [DataRow(" NOOP\r\n", "* BAD Invalid tag\r\n")]
    [DataRow("+ NOOP\r\n", "* BAD Invalid tag\r\n")]
    [DataRow("a(b NOOP\r\n", "* BAD Invalid tag\r\n")]
    [DataRow("a*b NOOP\r\n", "* BAD Invalid tag\r\n")]
    [DataRow("a] NOOP\r\n", "a] OK NOOP completed\r\n")]
    public async Task ServeAsync_Command_AnswersAsDecisionThreeSays(string request, string expected)
    {
        var responses = await ResponsesAsync(request, TestContext.CancellationToken);

        Assert.AreEqual(expected, responses);
    }

    [TestMethod]
    public async Task ServeAsync_Logout_SaysByeCompletesAndCloses()
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(Server(AnonymousStore(clock)), "a LOGOUT\r\nb NOOP\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual("* BYE surl logging out\r\na OK LOGOUT completed\r\n", AfterGreeting(connection));
        Assert.IsTrue(connection.WritesCompleted);
    }
}
