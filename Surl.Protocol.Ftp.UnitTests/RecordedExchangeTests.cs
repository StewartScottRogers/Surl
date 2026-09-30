using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ftp.FtpTestExchange;

namespace Surl.Protocol.Ftp;

/// <summary>
/// Replays each control-channel exchange recorded from pinned upstream curl (Fixtures/README.md)
/// and asserts surl answers every line with the reply the recorder fed curl, byte for byte.
/// </summary>
[TestClass]
public sealed class RecordedExchangeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("login-cwd-missing", "9", DisplayName = "Anonymous login, PWD, CWD to a missing directory, QUIT")]
    [DataRow("quote-type-cwd", "9", DisplayName = "Anonymous login, PWD, TYPE A, TYPE I, NOOP, SYST, CWD, CWD to a missing directory, QUIT")]
    public async Task Replay_AnonymousSession_SendsTheRecordedReplies(string caseName, string exitCode)
    {
        foreach (var chunks in Readings(caseName))
        {
            var connection = new InMemoryConnection(chunks);

            await Server().ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));

            Assert.AreEqual(RecordedFixture.ReadServerReplies(caseName), Text(connection.WrittenBytes));
            Assert.IsTrue(connection.WritesCompleted);
        }

        Assert.AreEqual(exitCode, Text(RecordedFixture.ReadBytes(caseName, "exitcode.txt")));
    }

    [TestMethod]
    public async Task Replay_LoginRefused_SendsTheRecordedRepliesAndNotesTheRefusal()
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.RefusedCredentials);
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([RecordedFixture.ReadRequestBytes("login-refused")]);

        await Server(policy).ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log));

        Assert.AreEqual(RecordedFixture.ReadServerReplies("login-refused"), Text(connection.WrittenBytes));
        Assert.AreEqual("67", Text(RecordedFixture.ReadBytes("login-refused", "exitcode.txt")));
        CollectionAssert.AreEqual(new[] { "Login refused: ftp tester" }, log.Notes.ToArray());
        var login = policy.Logins.Single();
        Assert.AreEqual("ftp", login.Scheme);
        Assert.AreEqual("tester", login.UserName);
        Assert.AreEqual("wrong", Encoding.ASCII.GetString(login.Password!.Value.Span));
        Assert.IsNull(login.TlsSession);
    }

    [TestMethod]
    public async Task Replay_LoginOverPlaintext_SendsTheRecordedRepliesWithNoNote()
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted) { RefuseWithoutTls = true };
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([RecordedFixture.ReadRequestBytes("login-refused-plaintext")]);

        await Server(policy).ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log));

        Assert.AreEqual(RecordedFixture.ReadServerReplies("login-refused-plaintext"), Text(connection.WrittenBytes));
        Assert.AreEqual("67", Text(RecordedFixture.ReadBytes("login-refused-plaintext", "exitcode.txt")));
        Assert.IsEmpty(log.Notes);
    }

    // The whole request at once, as a pipelining client sends it, and one byte per read.
    private static IEnumerable<IEnumerable<ReadOnlyMemory<byte>>> Readings(string caseName)
    {
        var request = RecordedFixture.ReadRequestBytes(caseName);

        return [RecordedFixture.Whole(request), RecordedFixture.OneBytePerRead(request)];
    }
}
