using System.Text;
using Surl.Content;
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
    [DataRow("upload-small", false, "0", true, 0L)]
    [DataRow("upload-small", true, "0", true, 0L)]
    [DataRow("upload-large", false, "0", true, 0L)]
    [DataRow("upload-large", true, "0", true, 0L)]
    [DataRow("upload-refused", false, "9", false, 0L)]
    [DataRow("upload-refused", true, "9", false, 0L)]
    [DataRow("upload-too-large", false, "25", true, 1000L)]
    [DataRow("upload-too-large", true, "25", true, 1000L)]
    public async Task ServeAsync_RecordedUploads_AreAnsweredWithTheBytesCurlWasFed(string caseName, bool oneBytePerRead, string curlExit, bool allowUploads, long maxUploadBytes)
    {
        var request = RecordedFixture.ReadBytes(caseName, "request.bin");
        var connection = new InMemoryConnection(oneBytePerRead ? RecordedFixture.OneBytePerRead(request) : RecordedFixture.Whole(request));
        var options = new ContentExposureOptions { AllowUploads = allowUploads, MaxUploadBytes = maxUploadBytes };
        var contentStore = new ContentStore(InMemoryContentFileSystem.RootPath, StandardFileSystem(), options);

        await Server(contentStore: contentStore).ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        Assert.AreEqual(curlExit, Encoding.ASCII.GetString(RecordedFixture.ReadBytes(caseName, "exitcode.txt")).Trim());
        CollectionAssert.AreEqual(RecordedFixture.ReadBytes(caseName, "response.bin"), connection.WrittenBytes);
    }

    [TestMethod]
    [DataRow("upload-small", false, 0L)]
    [DataRow("upload-large", false, 0L)]
    [DataRow("upload-refused", true, 0L)]
    [DataRow("upload-too-large", true, 1000L)]
    public async Task ServeAsync_RecordedUpload_StoresWhatCurlSentOrNothing(string caseName, bool storesNothing, long maxUploadBytes)
    {
        var fileSystem = StandardFileSystem();
        var shareEntries = fileSystem.EnumerateDirectoryEntryNames(Path.Join(InMemoryContentFileSystem.RootPath, "share")).ToArray();
        var options = new ContentExposureOptions { AllowUploads = caseName != "upload-refused", MaxUploadBytes = maxUploadBytes };
        var connection = new InMemoryConnection(RecordedFixture.Whole(RecordedFixture.ReadBytes(caseName, "request.bin")));

        await Server(contentStore: new ContentStore(InMemoryContentFileSystem.RootPath, fileSystem, options))
            .ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        var sent = caseName == "upload-small" ? Encoding.ASCII.GetBytes("upload me\n") : BigFileBytes();
        var stored = StoredBytes(fileSystem, "share", "up.txt");
        if (storesNothing)
        {
            Assert.IsNull(stored);
            CollectionAssert.AreEquivalent(shareEntries, fileSystem.EnumerateDirectoryEntryNames(Path.Join(InMemoryContentFileSystem.RootPath, "share")).ToArray());
        }
        else
        {
            CollectionAssert.AreEqual(sent, stored, $"share\\up.txt after {caseName}");
        }
    }

    [TestMethod]
    public async Task ServeAsync_RecordedLargeUpload_NotesTheOpenAndTheBytesWritten()
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(RecordedFixture.Whole(RecordedFixture.ReadBytes("upload-large", "request.bin")));

        await Server(contentStore: UploadContentStore(StandardFileSystem())).ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log));

        CollectionAssert.AreEqual(
            new[] { "Login accepted: ntlmv1 alice", "SMB tree connect share: connected", @"SMB open share\up.txt for writing", @"SMB close share\up.txt: 40000 bytes written" },
            log.Notes.ToArray());
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
