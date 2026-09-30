using Surl.Protocol.Abstractions;
using static Surl.Protocol.Pop3.Pop3TestExchange;

namespace Surl.Protocol.Pop3;

/// <summary>
/// ADR-0056 decisions 2 and 4: the greeting's <c>APOP</c> timestamp and <c>APOP</c> checked by
/// the policy against it (RFC 1939, section 7; ADR-0049, section 5).
/// </summary>
[TestClass]
public sealed class Pop3ApopTests
{
    private const string Timestamp = "<0123456789abcdef.1790668800@surl>";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_ApopOffered_GreetsWithAFreshTimestamp()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy { IsApopOffered = true };

        var connection = await ServeAsync(AccountStore(clock), string.Empty, clock, TestContext.CancellationToken, policy);

        Assert.AreEqual("+OK surl ready " + Timestamp + "\r\n", Utf8(connection.WrittenBytes));
    }

    // Without an injected generator the server draws the timestamp's bytes from the system's.
    [TestMethod]
    public async Task ServeAsync_DefaultRandomSource_GreetsWithATimestampOfTheDecidedForm()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy { IsApopOffered = true };
        var connection = new InMemoryConnection([]);

        await new Pop3ProtocolServer(policy, policy, AccountStore(clock)).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.MatchesRegex(@"^\+OK surl ready <[0-9a-f]{16}\.1790668800@surl>\r\n$", Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_ApopAccepted_HandsThePolicyTheTimestampAndOpensTheMaildrop()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var policy = new Pop3TestPolicy
        {
            IsApopOffered = true,
            Steps = [Pop3TestPolicy.Ended(MailLoginOutcome.Accepted, new CheckedLogin("APOP", "u", true))],
        };

        var connection = await ServeAsync(AccountStore(clock, Message), "APOP u 0123abcd\r\nSTAT\r\n", clock, TestContext.CancellationToken, policy, log: log);

        Assert.AreEqual("+OK surl ready " + Timestamp + "\r\n+OK Authentication successful\r\n+OK 1 133\r\n", Utf8(connection.WrittenBytes));
        var login = policy.ApopLogins.Single();
        Assert.AreEqual("pop3", login.Scheme);
        Assert.AreEqual("u", login.UserName);
        Assert.AreEqual(Timestamp, login.Timestamp);
        Assert.AreEqual("0123abcd", login.Digest);
        Assert.IsNull(login.TlsSession);
        CollectionAssert.AreEqual(new[] { "Login accepted: APOP u", "Maildrop opened: 1 messages, 133 octets" }, log.Notes.ToList());
    }

    [TestMethod]
    [DataRow(MailLoginOutcome.RefusedCredentials, "-ERR [AUTH] Authentication failed")]
    [DataRow(MailLoginOutcome.RefusedPlaintext, "-ERR [AUTH] Encryption required")]
    [DataRow(MailLoginOutcome.RefusedMechanism, "-ERR Unsupported authentication mechanism")]
    [DataRow(MailLoginOutcome.Challenge, "-ERR [AUTH] Authentication failed")]
    public async Task ServeAsync_ApopRefused_AnswersInPop3WordsAndStaysUnauthorized(MailLoginOutcome outcome, string reply)
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy { IsApopOffered = true, Steps = [Pop3TestPolicy.Ended(outcome)] };

        var connection = await ServeAsync(AccountStore(clock), "APOP u 0123abcd\r\nSTAT\r\n", clock, TestContext.CancellationToken, policy);

        Assert.AreEqual(
            "+OK surl ready " + Timestamp + "\r\n" + reply + "\r\n-ERR [AUTH] Authentication required\r\n",
            Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_ApopWithoutATimestamp_IsUnsupported()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy();

        var connection = await ServeAsync(AccountStore(clock), "APOP u 0123abcd\r\n", clock, TestContext.CancellationToken, policy);

        Assert.AreEqual("-ERR Unsupported authentication mechanism\r\n", RepliesAfterGreeting(connection));
        Assert.IsEmpty(policy.ApopLogins);
    }

    // The greeting's timestamp stays the session's after STLS, but APOP is refused once the
    // policy no longer offers it on the connection.
    [TestMethod]
    public async Task ServeAsync_ApopNoLongerOfferedAfterStls_IsUnsupported()
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy { IsApopOffered = true, IsApopOfferedOverTls = false };
        var connection = new InMemoryConnection(Ascii("STLS\r\n", "APOP u 0123abcd\r\n"));

        await Server(AccountStore(clock), policy, isTlsUpgradeAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(
            "+OK surl ready " + Timestamp + "\r\n+OK Begin TLS negotiation\r\n-ERR Unsupported authentication mechanism\r\n",
            Utf8(connection.WrittenBytes));
        Assert.IsEmpty(policy.ApopLogins);
    }

    [TestMethod]
    [DataRow("APOP u")]
    [DataRow("APOP")]
    [DataRow("APOP u d x")]
    [DataRow("APOP u  d")]
    public async Task ServeAsync_ApopWithoutANameAndADigest_AnswersInvalidArguments(string line)
    {
        var clock = new ManualTimeProvider();
        var policy = new Pop3TestPolicy { IsApopOffered = true };

        var connection = await ServeAsync(AccountStore(clock), line + "\r\n", clock, TestContext.CancellationToken, policy);

        Assert.EndsWith("\r\n-ERR Invalid arguments\r\n", Utf8(connection.WrittenBytes));
        Assert.IsEmpty(policy.ApopLogins);
    }
}
