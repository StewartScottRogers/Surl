using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ftp.FtpTestExchange;
using static Surl.Protocol.Ftp.FtpUploadTests;

namespace Surl.Protocol.Ftp;

/// <summary>
/// Replays each upload and file-management session recorded from pinned upstream curl
/// (Fixtures/README.md, "Uploads and file management (BL-180)") against a content store with
/// <c>--allow-uploads</c>, with an in-memory passive data connection on the port the recorder
/// announced carrying what curl sent, and asserts surl answers every control line with the
/// reply the recorder fed curl and the uploaded bytes land in the store.
/// </summary>
[TestClass]
public sealed class RecordedUploadTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("upload", "dir/b.txt", DisplayName = "-T: CWD dir, STOR b.txt")]
    [DataRow("upload-create-dirs", "new/deep/b.txt", DisplayName = "--ftp-create-dirs -T: CWD, MKD, CWD for each directory, then STOR")]
    public async Task Replay_Upload_WritesWhatCurlSent(string caseName, string uploadedPath)
    {
        var fileSystem = StandardFileSystem();
        var uploaded = RecordedFixture.ReadBytes(caseName, "upload.bin");

        var (control, dataConnection) = await ReplayAsync(caseName, Store(fileSystem), uploaded);

        Assert.AreEqual(RecordedFixture.ReadServerReplies(caseName), Text(control.WrittenBytes));
        Assert.AreEqual(Text(uploaded), ReadFile(fileSystem, uploadedPath.Split('/')));
        Assert.AreEqual("uploaded body\n", Text(uploaded));
        Assert.IsTrue(dataConnection.Disposed);
        Assert.AreEqual("0", ExitCode(caseName));
    }

    [TestMethod]
    public async Task Replay_ResumedUpload_AppendsWhatCurlSentAfterTheSizeSurlReported()
    {
        var fileSystem = StandardFileSystem();
        var uploaded = RecordedFixture.ReadBytes("upload-resume", "upload.bin");

        var (control, _) = await ReplayAsync("upload-resume", Store(fileSystem), uploaded);

        Assert.AreEqual(RecordedFixture.ReadServerReplies("upload-resume"), Text(control.WrittenBytes));
        Assert.AreEqual("hello world\nmore\n", ReadFile(fileSystem, "a.txt"));
        Assert.AreEqual("0", ExitCode("upload-resume"));
    }

    [TestMethod]
    public async Task Replay_UploadPastMaxFileSize_Answers552AndLeavesNothingBehind()
    {
        var fileSystem = StandardFileSystem();

        // curl sent the whole file; the recorder read 4 bytes of it before it closed.
        var (control, dataConnection) = await ReplayAsync(
            "upload-too-large", Store(fileSystem, maxUploadBytes: 4), RecordedFixture.ReadBytes("upload", "upload.bin"));

        Assert.AreEqual(RecordedFixture.ReadServerReplies("upload-too-large"), Text(control.WrittenBytes));
        CollectionAssert.AreEquivalent(StandardRootEntries, RootEntries(fileSystem));
        Assert.IsTrue(dataConnection.Aborted);
        Assert.AreEqual("70", ExitCode("upload-too-large"));
    }

    [TestMethod]
    public async Task Replay_UploadWithoutAllowUploads_Answers550BeforeAnyDataConnection()
    {
        var fileSystem = StandardFileSystem();

        var (control, dataConnection) = await ReplayAsync(
            "upload-not-permitted", Store(fileSystem, allowUploads: false), RecordedFixture.ReadBytes("upload", "upload.bin"));

        Assert.AreEqual(RecordedFixture.ReadServerReplies("upload-not-permitted"), Text(control.WrittenBytes));
        CollectionAssert.AreEquivalent(StandardRootEntries, RootEntries(fileSystem));
        Assert.IsFalse(dataConnection.Disposed);
        Assert.AreEqual("25", ExitCode("upload-not-permitted"));
    }

    [TestMethod]
    public async Task Replay_QuoteCommands_MakeRenameDeleteAndRemoveAroundTheDownload()
    {
        var fileSystem = StandardFileSystem();

        var (control, dataConnection) = await ReplayAsync("quote-file-management", Store(fileSystem), []);

        Assert.AreEqual(RecordedFixture.ReadServerReplies("quote-file-management"), Text(control.WrittenBytes));
        Assert.AreEqual(FileText, Text(dataConnection.WrittenBytes));
        Assert.AreEqual(FileText, Text(RecordedFixture.ReadBytes("quote-file-management", "stdout.bin")));
        CollectionAssert.AreEquivalent(new[] { ".hidden", ".secret.txt", ".surl", "dir" }, RootEntries(fileSystem));
        Assert.AreEqual("0", ExitCode("quote-file-management"));
    }

    private async Task<(InMemoryConnection Control, InMemoryConnection DataConnection)> ReplayAsync(
        string caseName, ContentStore contentStore, byte[] sentOnTheDataConnection)
    {
        var dataConnection = new InMemoryConnection([sentOnTheDataConnection]);
        var dataConnections = new InMemoryDataConnections()
            .ScriptPassiveListener(new IPEndPoint(IPAddress.Loopback, AnnouncedPassivePort(caseName)), dataConnection);
        var control = new InMemoryConnection([RecordedFixture.ReadRequestBytes(caseName)]);
        var context = Context(new ManualTimeProvider(), TestContext.CancellationToken, dataConnections: dataConnections);

        await Server(contentStore: contentStore).ServeAsync(control, context);

        Assert.IsTrue(control.WritesCompleted);
        return (control, dataConnection);
    }

    private static string ExitCode(string caseName) => Text(RecordedFixture.ReadBytes(caseName, "exitcode.txt"));

    private static int AnnouncedPassivePort(string caseName)
    {
        var transcript = Encoding.ASCII.GetString(RecordedFixture.ReadBytes(caseName, "transcript.txt"));
        var extended = Regex.Match(transcript, @"^< 229 .*\(\|\|\|(\d+)\|\)", RegexOptions.Multiline);
        return int.Parse(extended.Groups[1].Value, CultureInfo.InvariantCulture);
    }
}
