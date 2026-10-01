using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Smb.SmbTestExchange;

namespace Surl.Protocol.Smb;

/// <summary>
/// Replays the requests pinned upstream curl 8.21.0 sent (Fixtures/README.md) and checks surl
/// answers them with the bytes curl was fed, which ended each case with ADR-0073's exit code.
/// </summary>
[TestClass]
public sealed class RecordedExchangeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("login-tree-connect", false, "78")]
    [DataRow("login-tree-connect", true, "78")]
    [DataRow("login-refused", false, "67")]
    [DataRow("login-refused", true, "67")]
    [DataRow("unknown-share", false, "78")]
    [DataRow("unknown-share", true, "78")]
    [DataRow("download-small", false, "0")]
    [DataRow("download-small", true, "0")]
    [DataRow("download-large", false, "0")]
    [DataRow("download-large", true, "0")]
    public async Task ServeAsync_RecordedRequests_AreAnsweredWithTheBytesCurlWasFed(string caseName, bool oneBytePerRead, string curlExit)
    {
        var request = RecordedFixture.ReadBytes(caseName, "request.bin");
        var connection = new InMemoryConnection(oneBytePerRead ? RecordedFixture.OneBytePerRead(request) : RecordedFixture.Whole(request));

        await Server().ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        Assert.AreEqual(curlExit, Encoding.ASCII.GetString(RecordedFixture.ReadBytes(caseName, "exitcode.txt")).Trim());
        CollectionAssert.AreEqual(RecordedFixture.ReadBytes(caseName, "response.bin"), connection.WrittenBytes);
    }

    [TestMethod]
    [DataRow("download-small", "file.txt")]
    [DataRow("download-large", "big.bin")]
    public void RecordedDownload_WroteTheServedFileToCurlsStdout(string caseName, string fileName)
    {
        var served = fileName == "big.bin" ? BigFileBytes() : Encoding.ASCII.GetBytes("hello smb\n");

        var stdout = RecordedFixture.ReadBytes(caseName, "stdout.bin");

        CollectionAssert.AreEqual(served, stdout, $"curl's stdout for share\\{fileName}");
    }

    [TestMethod]
    public async Task ServeAsync_RecordedLargeDownload_NotesTheOpenAndTheBytesRead()
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(RecordedFixture.Whole(RecordedFixture.ReadBytes("download-large", "request.bin")));

        await Server().ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log));

        CollectionAssert.AreEqual(
            new[] { "Login accepted: ntlmv1 alice", "SMB tree connect share: connected", @"SMB open share\big.bin for reading: 40000 bytes", @"SMB close share\big.bin: 40000 bytes read" },
            log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_RecordedLogin_HandsThePolicyTheLoginAsSent()
    {
        var policy = new SecretPasswordPolicy();
        var connection = new InMemoryConnection(RecordedFixture.Whole(RecordedFixture.ReadBytes("login-tree-connect", "request.bin")));

        await Server(policy).ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        var login = policy.Logins.Single();
        Assert.AreEqual("alice", login.UserName);
        Assert.AreEqual("127.0.0.1", login.DomainName);
        CollectionAssert.AreEqual(Hex(ChallengeHex), login.ServerChallenge.ToArray());
        CollectionAssert.AreEqual(Hex("101C21228F73993193C75440547D94B75F3231384D879388"), login.LmResponse.ToArray());
        CollectionAssert.AreEqual(Hex(SecretNtResponseHex), login.NtResponse.ToArray());
        Assert.IsNull(login.TlsSession);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedLogin_NotesTheLoginTheTreeAndTheOpenAndNoSecret()
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(RecordedFixture.Whole(RecordedFixture.ReadBytes("login-tree-connect", "request.bin")));

        await Server().ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log));

        CollectionAssert.AreEqual(
            new[] { "Login accepted: ntlmv1 alice", "SMB tree connect share: connected", @"SMB open share\dir\file.txt refused: ERRbadfile" },
            log.Notes.ToArray());
        foreach (var note in log.Notes)
        {
            Assert.DoesNotContain("secret", note);
            Assert.DoesNotContain("2FECDD61", note, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("0123456789", note);
        }
    }

    [TestMethod]
    public async Task ServeAsync_RecordedRefusedLogin_NotesTheRefusalAndClosesAfterAnswering()
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(RecordedFixture.Whole(RecordedFixture.ReadBytes("login-refused", "request.bin")));

        await Server().ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log));

        CollectionAssert.AreEqual(new[] { "Login refused: ntlmv1 alice" }, log.Notes.ToArray());
        Assert.IsTrue(connection.WritesCompleted);
    }
}
