using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Pop3.Pop3TestExchange;

namespace Surl.Protocol.Pop3;

/// <summary>
/// Replays each upstream curl recording in <c>Fixtures/</c> (see its README): the bytes curl sent
/// against the replies ADR-0056 decides, which curl accepted with the exit code recorded. Surl,
/// serving a maildrop of two copies of the recorder's message, must write exactly the replies the
/// recorder sent.
/// </summary>
[TestClass]
public sealed class RecordedFixtureTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("list", "0", 2)]
    [DataRow("retr", "0", 2)]
    [DataRow("list-l", "0", 2)]
    [DataRow("list-one-l", "0", 2)]
    [DataRow("head-list", "0", 2)]
    [DataRow("head-retr", "0", 2)]
    [DataRow("x-uidl", "0", 2)]
    [DataRow("x-uidl-one", "0", 2)]
    [DataRow("x-capa", "0", 2)]
    [DataRow("x-list-one", "0", 2)]
    [DataRow("x-dele", "0", 1)]
    [DataRow("x-dele-url", "0", 1)]
    [DataRow("x-top", "0", 2)]
    [DataRow("x-top-url", "0", 2)]
    [DataRow("x-retr", "0", 2)]
    [DataRow("x-stat", "0", 2)]
    [DataRow("x-noop", "0", 2)]
    [DataRow("x-rset", "0", 2)]
    [DataRow("x-xyzzy", "8", 2)]
    [DataRow("retr-missing", "8", 2)]
    public async Task ServeAsync_RecordedRequest_WritesTheRecordedReplies(string caseName, string exitCode, int messagesLeft)
    {
        Assert.AreEqual(exitCode, Read(caseName, "exitcode.txt").Trim());
        var clock = new ManualTimeProvider();
        var store = AccountStore(clock, Message, Message);
        var connection = new InMemoryConnection([ReadBytes(caseName, "request.bin")]);

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedReplies(caseName), Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.HasCount(messagesLeft, Inbox(store, "u"));
    }

    // curl without -u sends RETR straight after CAPA; --allow-anonymous lets it read the
    // anonymous owner's maildrop (ADR-0056, decision 7).
    [TestMethod]
    public async Task ServeAsync_RecordedRequestWithoutLogin_ReadsTheAnonymousMaildrop()
    {
        Assert.AreEqual("0", Read("anonymous-retr", "exitcode.txt").Trim());
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock, Message, Message);
        var connection = new InMemoryConnection([ReadBytes("anonymous-retr", "request.bin")]);
        var policy = new Pop3TestPolicy { AnonymousVerdict = PasswordLoginVerdict.AcceptedUnchecked };

        await Server(store, policy).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedReplies("anonymous-retr"), Utf8(connection.WrittenBytes));
        Assert.AreEqual(Message, Utf8(ReadBytes("anonymous-retr", "stdout.bin")));
    }

    [TestMethod]
    public async Task ServeAsync_RecordedRequestOneBytePerRead_WritesTheRecordedReplies()
    {
        var clock = new ManualTimeProvider();
        var store = AccountStore(clock, Message, Message);
        var request = ReadBytes("retr", "request.bin");
        var connection = new InMemoryConnection(Enumerable.Range(0, request.Length).Select(index => new ReadOnlyMemory<byte>(request, index, 1)));

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedReplies("retr"), Utf8(connection.WrittenBytes));
    }

    // curl --ssl-reqd: STLS offered only before TLS, USER only after it (ADR-0056 row 27). The
    // request is handed out in two reads split after the STLS line, and the upgrade must come
    // straight after the +OK.
    [TestMethod]
    public async Task ServeAsync_RecordedStlsRequest_UpgradesAfterTheOkAndWritesTheRecordedReplies()
    {
        Assert.AreEqual("0", Read("stls", "exitcode.txt").Trim());
        var clock = new ManualTimeProvider();
        var store = AccountStore(clock, Message, Message);
        var request = ReadBytes("stls", "request.bin");
        var upgradePoint = Encoding.ASCII.GetString(request).IndexOf("STLS\r\n", StringComparison.Ordinal) + "STLS\r\n".Length;
        var connection = new UpgradePointRecordingConnection([request.AsMemory(0, upgradePoint), request.AsMemory(upgradePoint)], plaintextChunkCount: 1);
        var policy = new Pop3TestPolicy { IsClearPasswordOffered = false, IsClearPasswordOfferedOverTls = true };

        await Server(store, policy, isTlsUpgradeAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        var replies = RecordedReplies("stls");
        Assert.AreEqual(replies, Utf8(connection.WrittenBytes));
        Assert.AreEqual(replies.IndexOf("+OK Begin TLS negotiation\r\n", StringComparison.Ordinal) + "+OK Begin TLS negotiation\r\n".Length, connection.WrittenBytesAtUpgrade);
        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, policy.Logins.Single().TlsSession);
    }

    // curl --ssl-reqd against a server with no certificate: CAPA offers no STLS, and curl gives
    // up with exit 64 (ADR-0056 row 30).
    [TestMethod]
    public async Task ServeAsync_RecordedStlsNotOffered_WritesTheRecordedReplies()
    {
        Assert.AreEqual("64", Read("stls-not-offered", "exitcode.txt").Trim());
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection([ReadBytes("stls-not-offered", "request.bin")]);
        var policy = new Pop3TestPolicy { IsClearPasswordOffered = false };

        await Server(AccountStore(clock), policy).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedReplies("stls-not-offered"), Utf8(connection.WrittenBytes));
    }

    // curl pop3s://: TLS from the first byte, which the engine completes before ServeAsync, so
    // CAPA offers no STLS even on a server that could upgrade (ADR-0056 row 31).
    [TestMethod]
    public async Task ServeAsync_RecordedPop3sRequest_WritesTheRecordedReplies()
    {
        Assert.AreEqual("0", Read("pop3s", "exitcode.txt").Trim());
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection([ReadBytes("pop3s", "request.bin")], initialTlsSession: InMemoryConnection.DefaultUpgradeTlsSession);

        await Server(AccountStore(clock, Message, Message), isTlsUpgradeAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedReplies("pop3s"), Utf8(connection.WrittenBytes));
        Assert.IsFalse(connection.UpgradeRequested);
    }

    // curl -u user:secret with a timestamp in the greeting: APOP, unforced when CAPA offers no
    // SASL (ADR-0056 D6), forced with --login-options AUTH=+APOP beside SASL (row 32). The clock
    // and the random bytes give the recorded timestamp, so the recorded digest is the one sent.
    [TestMethod]
    [DataRow("apop", "0", false, MailLoginOutcome.Accepted)]
    [DataRow("apop-forced", "0", true, MailLoginOutcome.Accepted)]
    [DataRow("apop-refused", "67", false, MailLoginOutcome.RefusedCredentials)]
    public async Task ServeAsync_RecordedApopRequest_WritesTheRecordedReplies(string caseName, string exitCode, bool offersMore, MailLoginOutcome outcome)
    {
        Assert.AreEqual(exitCode, Read(caseName, "exitcode.txt").Trim());
        var clock = new ManualTimeProvider(DateTimeOffset.FromUnixTimeSeconds(1790640000));
        var connection = new InMemoryConnection([ReadBytes(caseName, "request.bin")]);
        var policy = new Pop3TestPolicy
        {
            IsApopOffered = true,
            IsClearPasswordOffered = offersMore,
            SaslMechanisms = offersMore ? ["CRAM-MD5", "PLAIN"] : [],
            Steps = [Pop3TestPolicy.Ended(outcome)],
        };

        await Server(AccountStore(clock, Message, Message), policy).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedReplies(caseName), Utf8(connection.WrittenBytes));
        var login = policy.ApopLogins.Single();
        Assert.AreEqual("user", login.UserName);
        Assert.AreEqual("<0123456789abcdef.1790640000@surl>", login.Timestamp);
        Assert.AreEqual("32d4437494fda0ae78d0559952474e34", login.Digest);
    }

    // curl -u user:secret --login-options AUTH=<mechanism> [--sasl-ir], against a CAPA offering
    // that mechanism alone: the policy is scripted with the challenges the recorder sent, then
    // the acceptance or, for auth-refused, the refusal (ADR-0049 section 7).
    [TestMethod]
    [DataRow("auth-plain", "PLAIN", "0")]
    [DataRow("auth-plain-sasl-ir", "PLAIN", "0")]
    [DataRow("auth-login", "LOGIN", "0")]
    [DataRow("auth-login-sasl-ir", "LOGIN", "0")]
    [DataRow("auth-cram-md5", "CRAM-MD5", "0")]
    [DataRow("auth-digest-md5", "DIGEST-MD5", "0")]
    [DataRow("auth-ntlm", "NTLM", "0")]
    [DataRow("auth-ntlm-sasl-ir", "NTLM", "0")]
    [DataRow("auth-xoauth2", "XOAUTH2", "0")]
    [DataRow("auth-xoauth2-sasl-ir", "XOAUTH2", "0")]
    [DataRow("auth-oauthbearer", "OAUTHBEARER", "0")]
    [DataRow("auth-external", "EXTERNAL", "0")]
    [DataRow("auth-external-sasl-ir", "EXTERNAL", "0")]
    [DataRow("auth-refused", "CRAM-MD5", "67")]
    public async Task ServeAsync_RecordedAuthRequest_WritesTheRecordedReplies(string caseName, string mechanism, string exitCode)
    {
        Assert.AreEqual(exitCode, Read(caseName, "exitcode.txt").Trim());
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection([ReadBytes(caseName, "request.bin")]);
        var challenges = RecordedChallenges(caseName);
        var outcome = exitCode == "0" ? MailLoginOutcome.Accepted : MailLoginOutcome.RefusedCredentials;
        var policy = new Pop3TestPolicy
        {
            IsClearPasswordOffered = false,
            SaslMechanisms = [mechanism],
            Steps = [.. challenges.Select(challenge => Pop3TestPolicy.Challenge(challenge)), Pop3TestPolicy.Ended(outcome)],
        };

        await Server(AccountStore(clock, Message, Message), policy).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedReplies(caseName), Utf8(connection.WrittenBytes));
        Assert.AreEqual(mechanism, policy.Starts.Single().Mechanism);
        Assert.HasCount(challenges.Count, policy.Responses);
    }

    // The challenges the recorder sent, decoded: every "+ " continuation line.
    private static List<byte[]> RecordedChallenges(string caseName) =>
        Read(caseName, "transcript.txt")
            .Split("\r\n")
            .Where(line => line.StartsWith("< + ", StringComparison.Ordinal))
            .Select(line => Convert.FromBase64String(line[4..]))
            .ToList();

    // Every line the recorder sent, in order, each ending CRLF.
    private static string RecordedReplies(string caseName) =>
        string.Concat(Read(caseName, "transcript.txt")
            .Split("\r\n")
            .Where(line => line.StartsWith("< ", StringComparison.Ordinal))
            .Select(line => line[2..] + "\r\n"));

    private static string Read(string caseName, string fileName) => Encoding.UTF8.GetString(ReadBytes(caseName, fileName));

    private static byte[] ReadBytes(string caseName, string fileName)
    {
        using var stream = typeof(RecordedFixtureTests).Assembly.GetManifestResourceStream($"Fixtures/{caseName}/{fileName}")
            ?? throw new InvalidOperationException($"No embedded fixture Fixtures/{caseName}/{fileName}.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
