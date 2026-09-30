using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Smtp.SmtpTestExchange;

namespace Surl.Protocol.Smtp;

/// <summary>
/// Replays each upstream curl recording in <c>Fixtures/</c> (see its README): the bytes curl sent
/// against the replies ADR-0053 decides, which curl accepted with the exit code recorded. Surl
/// must write exactly the replies the recorder sent, and store exactly the message curl sent,
/// after decision 6's trace fields.
/// </summary>
[TestClass]
public sealed class RecordedFixtureTests
{
    private const string Mail = "From: a@x\r\nTo: b@y\r\nSubject: hi\r\n\r\nhello\r\n.dot line\r\n";
    private const string Dots = "Subject: dots\r\n\r\n.\r\n..\r\n.x\r\nend\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("one-recipient", "0", 1, Mail)]
    [DataRow("two-recipients", "0", 2, Mail)]
    [DataRow("refused-recipient", "55", 0, "")]
    [DataRow("refused-recipient-allowfails", "0", 1, Mail)]
    [DataRow("dot-stuffed-body", "0", 1, Dots)]
    [DataRow("vrfy", "0", 0, "")]
    [DataRow("expn", "0", 0, "")]
    [DataRow("help", "0", 0, "")]
    [DataRow("noop", "0", 0, "")]
    public async Task ServeAsync_RecordedRequest_WritesTheRecordedRepliesAndStoresTheMessage(string caseName, string exitCode, int storedCopies, string body)
    {
        Assert.AreEqual(exitCode, Read(caseName, "exitcode.txt").Trim());
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        var connection = new InMemoryConnection([ReadBytes(caseName, "request.bin")]);

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedReplies(caseName), Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        var expected = Enumerable.Repeat(TraceFields("a@x") + body, storedCopies).ToList();
        CollectionAssert.AreEqual(expected, Inbox(store, string.Empty).ToList());
    }

    [TestMethod]
    public async Task ServeAsync_RecordedRequestOneBytePerRead_WritesTheRecordedReplies()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        var request = ReadBytes("dot-stuffed-body", "request.bin");
        var connection = new InMemoryConnection(Enumerable.Range(0, request.Length).Select(index => new ReadOnlyMemory<byte>(request, index, 1)));

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedReplies("dot-stuffed-body"), Utf8(connection.WrittenBytes));
        CollectionAssert.AreEqual(new[] { TraceFields("a@x") + Dots }, Inbox(store, string.Empty).ToList());
    }

    // curl --ssl-reqd: EHLO, STARTTLS, then everything after the handshake, which the connection
    // hands out only once the server has upgraded (ADR-0053 row 40).
    [TestMethod]
    public async Task ServeAsync_RecordedStartTlsRequest_UpgradesAfter220AndWritesTheRecordedReplies()
    {
        Assert.AreEqual("0", Read("starttls", "exitcode.txt").Trim());
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        var request = ReadBytes("starttls", "request.bin");
        var upgradePoint = Encoding.ASCII.GetString(request).IndexOf("STARTTLS\r\n", StringComparison.Ordinal) + "STARTTLS\r\n".Length;
        var connection = new UpgradePointRecordingConnection([request.AsMemory(0, upgradePoint), request.AsMemory(upgradePoint)], plaintextChunkCount: 1);

        await StartTlsServer(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedReplies("starttls"), Utf8(connection.WrittenBytes));
        Assert.AreEqual((Greeting + EhloReplyWithStartTls + "220 2.0.0 Ready to start TLS\r\n").Length, connection.WrittenBytesAtUpgrade);
        Assert.IsTrue(connection.WritesCompleted);
        CollectionAssert.AreEqual(new[] { TraceFields("a@x", protocol: "ESMTPS") + Mail }, Inbox(store, string.Empty).ToList());
    }

    // curl smtps://: TLS from the first byte, which the engine completes before ServeAsync (ADR-0053 row 44).
    [TestMethod]
    public async Task ServeAsync_RecordedSmtpsRequest_WritesTheRecordedRepliesWithoutStartTls()
    {
        Assert.AreEqual("0", Read("smtps", "exitcode.txt").Trim());
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        var connection = new InMemoryConnection([ReadBytes("smtps", "request.bin")], initialTlsSession: InMemoryConnection.DefaultUpgradeTlsSession);

        await StartTlsServer(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedReplies("smtps"), Utf8(connection.WrittenBytes));
        Assert.IsFalse(connection.UpgradeRequested);
        Assert.IsTrue(connection.WritesCompleted);
        CollectionAssert.AreEqual(new[] { TraceFields("a@x", protocol: "ESMTPS") + Mail }, Inbox(store, string.Empty).ToList());
    }

    // curl -u ... --login-options AUTH=<mechanism> [--sasl-ir]: the policy double is scripted with
    // the challenges the recorder sent and then accepts, so Surl must frame them into exactly the
    // recorded 334 lines, hand the policy exactly what curl sent, answer 235 and let the mail in
    // though the session has no --allow-anonymous (ADR-0049 section 7, ADR-0053 decision 3).
    [TestMethod]
    [DataRow("auth-plain", "PLAIN")]
    [DataRow("auth-plain-sasl-ir", "PLAIN")]
    [DataRow("auth-login", "LOGIN")]
    [DataRow("auth-login-sasl-ir", "LOGIN")]
    [DataRow("auth-cram-md5", "CRAM-MD5")]
    [DataRow("auth-cram-md5-sasl-ir", "CRAM-MD5")]
    [DataRow("auth-digest-md5", "DIGEST-MD5")]
    [DataRow("auth-digest-md5-sasl-ir", "DIGEST-MD5")]
    [DataRow("auth-ntlm", "NTLM")]
    [DataRow("auth-ntlm-sasl-ir", "NTLM")]
    [DataRow("auth-xoauth2", "XOAUTH2")]
    [DataRow("auth-xoauth2-sasl-ir", "XOAUTH2")]
    [DataRow("auth-oauthbearer", "OAUTHBEARER")]
    [DataRow("auth-oauthbearer-sasl-ir", "OAUTHBEARER")]
    [DataRow("auth-external", "EXTERNAL")]
    [DataRow("auth-external-sasl-ir", "EXTERNAL")]
    public async Task ServeAsync_RecordedAuthRequest_RelaysTheChallengesAndWritesTheRecordedReplies(string caseName, string mechanism)
    {
        Assert.AreEqual("0", Read(caseName, "exitcode.txt").Trim());
        var transcript = Read(caseName, "transcript.txt").Split("\r\n");
        var authIndex = Array.FindIndex(transcript, line => line.StartsWith("> AUTH ", StringComparison.Ordinal));
        var exchange = transcript.Skip(authIndex + 1).TakeWhile(line => !line.StartsWith("< 235", StringComparison.Ordinal)).ToList();
        var challenges = exchange.Where(line => line.StartsWith("< 334 ", StringComparison.Ordinal)).Select(line => Convert.FromBase64String(line[6..]));
        var sentResponses = exchange.Where(line => line.StartsWith("> ", StringComparison.Ordinal)).Select(line => Convert.FromBase64String(line[2..])).ToList();
        var authWords = transcript[authIndex][2..].Split(' ');
        var steps = challenges.Select(challenge => ScriptedMailAuthenticationPolicy.Challenge(challenge))
            .Append(ScriptedMailAuthenticationPolicy.Ended(MailLoginOutcome.Accepted, new CheckedLogin(mechanism, "user", true)))
            .ToArray();
        var mailPolicy = new ScriptedMailAuthenticationPolicy([mechanism], null, steps);
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([ReadBytes(caseName, "request.bin")]);

        await new SmtpProtocolServer(new UnitTestRefusingPolicy(), mailPolicy, store).ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));

        Assert.AreEqual(RecordedReplies(caseName), Utf8(connection.WrittenBytes));
        var start = mailPolicy.Starts.Single();
        Assert.AreEqual(mechanism, start.Mechanism);
        Assert.AreEqual(authWords.Length == 3 ? Convert.ToBase64String(Convert.FromBase64String(authWords[2])) : null, start.InitialResponse is { } initial ? Convert.ToBase64String(initial.Span) : null);
        CollectionAssert.AreEqual(sentResponses.Select(Convert.ToBase64String).ToList(), mailPolicy.Responses.Select(Convert.ToBase64String).ToList());
        CollectionAssert.Contains(log.Notes.ToList(), $"Login accepted: {mechanism} user");
        CollectionAssert.AreEqual(new[] { TraceFields("a@x", protocol: "ESMTPA") + Mail }, Inbox(store, string.Empty).ToList());
    }

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
