using System.Text;
using Surl.MailStore;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Imap.ImapTestExchange;

namespace Surl.Protocol.Imap;

/// <summary>
/// Replays each upstream curl recording in <c>Fixtures/</c> (see its README): the bytes curl sent
/// against the responses ADR-0055 decides, which curl accepted with exit 0. Surl must write
/// exactly the responses the recorder sent, against a store whose <c>INBOX</c> holds two unseen
/// messages beside a mailbox <c>Sent</c>.
/// </summary>
[TestClass]
public sealed class RecordedFixtureTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("list-root")]
    [DataRow("list-inbox")]
    [DataRow("lsub")]
    [DataRow("select")]
    [DataRow("examine")]
    [DataRow("status")]
    [DataRow("fetch-uid")]
    [DataRow("fetch-mailindex")]
    [DataRow("fetch-section-text")]
    [DataRow("fetch-header-fields")]
    [DataRow("fetch-section-1")]
    [DataRow("fetch-partial")]
    [DataRow("fetch-text-partial")]
    [DataRow("search-subject")]
    [DataRow("fetch-all")]
    [DataRow("uid-fetch-bodystructure")]
    [DataRow("uid-search")]
    public async Task ServeAsync_RecordedRequest_WritesTheRecordedResponses(string caseName)
    {
        Assert.AreEqual("0", Read(caseName, "exitcode.txt").Trim());
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 2);
        Create(store, string.Empty, "Sent");
        var connection = new InMemoryConnection([ReadBytes(caseName, "request.bin")]);

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedResponses(caseName), Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    [DataRow("append", "INBOX[1 None, 2 None, 3 Seen] Sent[]")]
    [DataRow("create", "Archive[] INBOX[1 None, 2 None] Sent[]")]
    [DataRow("delete", "INBOX[1 None, 2 None]")]
    [DataRow("rename", "Archive[] INBOX[1 None, 2 None]")]
    [DataRow("subscribe", "INBOX[1 None, 2 None] Sent[]")]
    [DataRow("unsubscribe", "INBOX[1 None, 2 None] Sent[]")]
    [DataRow("store", "INBOX[1 Deleted, 2 None] Sent[]")]
    [DataRow("uid-store-silent", "INBOX[1 Seen, 2 Seen] Sent[]")]
    [DataRow("copy", "INBOX[1 None, 2 None] Sent[1 None, 2 None]")]
    [DataRow("uid-move", "INBOX[2 None] Sent[1 None]")]
    [DataRow("expunge", "INBOX[2 None] Sent[]")]
    [DataRow("uid-expunge", "INBOX[2 None] Sent[]")]
    public async Task ServeAsync_RecordedChange_WritesTheRecordedResponsesAndChangesTheStore(string caseName, string expectedStore)
    {
        Assert.AreEqual("0", Read(caseName, "exitcode.txt").Trim());
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 2);
        Create(store, string.Empty, "Sent");
        if (caseName.EndsWith("expunge", StringComparison.Ordinal))
        {
            store.ChangeFlags(store.ViewFor(null), "INBOX", 1, MailFlagChange.Add, MailFlags.Deleted, out _);
        }

        var connection = new InMemoryConnection([ReadBytes(caseName, "request.bin")]);

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedResponses(caseName), Utf8(connection.WrittenBytes));
        Assert.AreEqual(expectedStore, Describe(store));
    }

    [TestMethod]
    public async Task ServeAsync_RecordedAppend_StoresTheUploadedBytesExactly()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 2);
        var connection = new InMemoryConnection([ReadBytes("append", "request.bin")]);

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(MailStoreOutcome.Succeeded, store.FetchMessage(store.ViewFor(null), "INBOX", 3, out var message));
        Assert.AreEqual("From: a@x\r\nSubject: hi\r\n\r\nhello\r\n", Encoding.UTF8.GetString(message.Span));
    }

    [TestMethod]
    public async Task ServeAsync_RecordedRequestOneBytePerRead_WritesTheRecordedResponses()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 2);
        Create(store, string.Empty, "Sent");
        var request = ReadBytes("status", "request.bin");
        var connection = new InMemoryConnection(Enumerable.Range(0, request.Length).Select(index => new ReadOnlyMemory<byte>(request, index, 1)));

        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedResponses("status"), Utf8(connection.WrittenBytes));
    }

    // curl --ssl-reqd: STARTTLS offered only before TLS; after it the capabilities are asked again
    // and curl logs in (ADR-0055 decision 11, row 44). The request is handed out in two reads split
    // after the STARTTLS line, and the upgrade must come straight after the OK.
    [TestMethod]
    [DataRow("starttls", "0", true, null)]
    [DataRow("starttls-logindisabled-remembered", "67", false, null)]
    [DataRow("starttls-authenticate", "0", false, "CRAM-MD5")]
    public async Task ServeAsync_RecordedStartTlsRequest_UpgradesAfterTheOkAndWritesTheRecordedResponses(string caseName, string exitCode, bool isClearPasswordOffered, string? mechanism)
    {
        Assert.AreEqual(exitCode, Read(caseName, "exitcode.txt").Trim());
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 2);
        Create(store, string.Empty, "Sent");
        var request = ReadBytes(caseName, "request.bin");
        var upgradePoint = Encoding.ASCII.GetString(request).IndexOf("STARTTLS\r\n", StringComparison.Ordinal) + "STARTTLS\r\n".Length;
        var connection = new UpgradePointRecordingConnection([request.AsMemory(0, upgradePoint), request.AsMemory(upgradePoint)], plaintextChunkCount: 1);
        var policy = new ScriptedLoginPolicy(PasswordLoginVerdict.Accepted, PasswordLoginVerdict.RefusedAnonymous, isClearPasswordOffered)
        {
            IsClearPasswordLoginOfferedOverTls = true,
            SaslMechanisms = mechanism is null ? [] : [mechanism],
            SaslMechanismsOverTls = mechanism is null ? [] : ["CRAM-MD5", "OAUTHBEARER", "XOAUTH2", "PLAIN", "LOGIN"],
            Steps = [.. RecordedChallenges(caseName).Select(challenge => ScriptedLoginPolicy.Challenge(challenge)), ScriptedLoginPolicy.Ended(SaslLoginOutcome.Accepted)],
        };

        await Server(store, policy, isTlsUpgradeAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        var responses = RecordedResponses(caseName);
        Assert.AreEqual(responses, Utf8(connection.WrittenBytes));
        var beginTls = "A002 OK Begin TLS negotiation now\r\n";
        Assert.AreEqual(responses.IndexOf(beginTls, StringComparison.Ordinal) + beginTls.Length, connection.WrittenBytesAtUpgrade);
    }

    // The same curl, against a server with no certificate: the capabilities offer no STARTTLS,
    // and curl gives up with exit 64 (ADR-0055 row 47).
    [TestMethod]
    public async Task ServeAsync_RecordedStartTlsNotOffered_WritesTheRecordedResponses()
    {
        Assert.AreEqual("64", Read("starttls-not-offered", "exitcode.txt").Trim());
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection([ReadBytes("starttls-not-offered", "request.bin")]);

        await Server(AnonymousStore(clock)).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedResponses("starttls-not-offered"), Utf8(connection.WrittenBytes));
    }

    // curl imaps://: TLS from the first byte, which the engine completes before ServeAsync, so the
    // capabilities offer no STARTTLS even on a server that could upgrade (ADR-0055 row 48).
    [TestMethod]
    public async Task ServeAsync_RecordedImapsRequest_WritesTheRecordedResponses()
    {
        Assert.AreEqual("0", Read("imaps", "exitcode.txt").Trim());
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 2);
        Create(store, string.Empty, "Sent");
        var connection = new InMemoryConnection([ReadBytes("imaps", "request.bin")], initialTlsSession: InMemoryConnection.DefaultUpgradeTlsSession);
        var policy = new ScriptedLoginPolicy(isClearPasswordLoginOffered: false) { IsClearPasswordLoginOfferedOverTls = true };

        await Server(store, policy, isTlsUpgradeAvailable: true).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedResponses("imaps"), Utf8(connection.WrittenBytes));
        Assert.IsFalse(connection.UpgradeRequested);
        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, policy.Logins.Single().TlsSession);
    }

    // curl -u <credentials> --login-options AUTH=<mechanism>, against capabilities offering that
    // mechanism alone and SASL-IR, so curl sends an initial response wherever its mechanism has
    // one (ADR-0055 D3): the policy is scripted with the challenges the recorder sent, then the
    // acceptance or, for authenticate-refused, the refusal (ADR-0049 section 7).
    [TestMethod]
    [DataRow("authenticate-plain", "PLAIN", "0", true)]
    [DataRow("authenticate-login", "LOGIN", "0", true)]
    [DataRow("authenticate-cram-md5", "CRAM-MD5", "0", false)]
    [DataRow("authenticate-digest-md5", "DIGEST-MD5", "0", false)]
    [DataRow("authenticate-ntlm", "NTLM", "0", true)]
    [DataRow("authenticate-xoauth2", "XOAUTH2", "0", true)]
    [DataRow("authenticate-oauthbearer", "OAUTHBEARER", "0", true)]
    [DataRow("authenticate-external", "EXTERNAL", "0", true)]
    [DataRow("authenticate-refused", "CRAM-MD5", "67", false)]
    public async Task ServeAsync_RecordedAuthenticateRequest_WritesTheRecordedResponses(string caseName, string mechanism, string exitCode, bool hasInitialResponse)
    {
        Assert.AreEqual(exitCode, Read(caseName, "exitcode.txt").Trim());
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        Deliver(store, string.Empty, 2);
        Create(store, string.Empty, "Sent");
        var connection = new InMemoryConnection([ReadBytes(caseName, "request.bin")]);
        var challenges = RecordedChallenges(caseName);
        var outcome = exitCode == "0" ? SaslLoginOutcome.Accepted : SaslLoginOutcome.RefusedCredentials;
        var policy = new ScriptedLoginPolicy(isClearPasswordLoginOffered: false)
        {
            SaslMechanisms = [mechanism],
            Steps = [.. challenges.Select(challenge => ScriptedLoginPolicy.Challenge(challenge)), ScriptedLoginPolicy.Ended(outcome)],
        };

        await Server(store, policy).ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        Assert.AreEqual(RecordedResponses(caseName), Utf8(connection.WrittenBytes));
        var start = policy.Starts.Single();
        Assert.AreEqual(mechanism, start.Mechanism);
        Assert.AreEqual(hasInitialResponse, start.InitialResponse.HasValue);
        Assert.HasCount(challenges.Count, policy.Responses);
    }

    // The challenges the recorder sent, decoded: every "+ " continuation line but the literal's.
    private static List<byte[]> RecordedChallenges(string caseName) =>
        Read(caseName, "transcript.txt")
            .Split("\r\n")
            .Where(line => line.StartsWith("< + ", StringComparison.Ordinal))
            .Select(line => Convert.FromBase64String(line[4..]))
            .ToList();

    // Every line the recorder sent, in order, each ending CRLF.
    private static string RecordedResponses(string caseName) =>
        string.Concat(Read(caseName, "transcript.txt")
            .Split("\r\n")
            .Where(line => line.StartsWith("< ", StringComparison.Ordinal))
            .Select(line => line[2..] + "\r\n"));

    // Every mailbox in the store's order, each with its messages' UIDs and flags.
    private static string Describe(MailboxStore store)
    {
        var view = store.ViewFor(null);
        return string.Join(' ', store.ListMailboxes(view).Select(name =>
        {
            store.ReadMailbox(view, name, out var snapshot);
            return $"{name}[{string.Join(", ", snapshot!.Messages.Select(message => $"{message.Uid} {message.Flags}"))}]";
        }));
    }

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
