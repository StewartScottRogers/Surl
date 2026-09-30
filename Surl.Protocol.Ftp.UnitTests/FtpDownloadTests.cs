using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ftp.FtpTestExchange;

namespace Surl.Protocol.Ftp;

/// <summary>
/// <c>SIZE</c>, <c>MDTM</c>, <c>REST</c>, <c>RETR</c> and <c>ABOR</c> (ADR-0052, decisions 1 and 4),
/// over a passive data connection from <see cref="InMemoryDataConnections"/>.
/// </summary>
[TestClass]
public sealed class FtpDownloadTests
{
    private const string PassiveReply = "229 Entering Extended Passive Mode (|||50100|)\r\n";
    private const string NoSuchFile = "550 No such file\r\n";
    private const string WholeFileOpening = "150 Opening data connection for a.txt (12 bytes)\r\n";
    private const string TransferComplete = "226 Transfer complete\r\n";

    private static readonly IPEndPoint PassiveEndPoint = new(IPAddress.Loopback, 50100);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("RETR missing.txt", DisplayName = "A missing file")]
    [DataRow("RETR .secret.txt", DisplayName = "A hidden file")]
    [DataRow("RETR .hidden/b.txt", DisplayName = "A file in a hidden directory")]
    [DataRow("RETR /.surl/state.txt", DisplayName = "A file in the service-state folder")]
    [DataRow("RETR .SURL/state.txt", DisplayName = "A file in the service-state folder in another case")]
    [DataRow("RETR dir", DisplayName = "A directory")]
    [DataRow("RETR /", DisplayName = "The root")]
    [DataRow("RETR ../a.txt", DisplayName = "Above the root")]
    [DataRow("RETR c:", DisplayName = "A name the content store refuses")]
    public async Task Retr_AnythingButAnExposedFile_Answers550BeforeAnyDataConnection(string command)
    {
        var dataConnections = PassiveDataConnections(new InMemoryConnection([]));

        var written = await ServeLoggedInAsync($"EPSV\r\n{command}\r\n", TestContext.CancellationToken, dataConnections);

        Assert.AreEqual(PassiveReply + NoSuchFile, written);
        Assert.IsEmpty(dataConnections.PassiveListeners.Single().AcceptTimeouts);
    }

    [TestMethod]
    public async Task Retr_PathNotUtf8_Answers550()
    {
        byte[] line = [.. "RETR a"u8, 0xFF, (byte)'\r', (byte)'\n'];
        var connection = new InMemoryConnection([Encoding.ASCII.GetBytes(AnonymousLogin), line]);

        await Server().ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        Assert.AreEqual(Greeting + AnonymousLoginReplies + NoSuchFile, Text(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("SIZE a.txt", "213 12")]
    [DataRow("SIZE dir/../a.txt", "213 12")]
    [DataRow("SIZE dir", "550 No such file")]
    [DataRow("SIZE .secret.txt", "550 No such file")]
    [DataRow("SIZE /.surl/state.txt", "550 No such file")]
    [DataRow("MDTM a.txt", "213 20260927123456")]
    [DataRow("MDTM missing.txt", "550 No such file")]
    [DataRow("SIZE", "501 Syntax error in arguments")]
    [DataRow("MDTM", "501 Syntax error in arguments")]
    [DataRow("REST", "501 Syntax error in arguments")]
    [DataRow("RETR", "501 Syntax error in arguments")]
    [DataRow("REST 5", "350 Restarting at 5")]
    [DataRow("REST 0", "350 Restarting at 0")]
    [DataRow("REST 12a", "501 Invalid restart offset")]
    [DataRow("REST -1", "501 Invalid restart offset")]
    [DataRow("REST 99999999999999999999", "501 Invalid restart offset")]
    [DataRow("ABOR", "226 Abort successful")]
    [DataRow("RETR a.txt", "425 Use PASV or PORT first")]
    public async Task Command_NeedingNoDataConnection_IsAnsweredFromTheTable(string command, string reply)
    {
        var written = await ServeLoggedInAsync(command + "\r\n", TestContext.CancellationToken);

        Assert.AreEqual(reply + "\r\n", written);
    }

    [TestMethod]
    public async Task Retr_ByRelativePath_EchoesThePathAsSent()
    {
        var dataConnection = new InMemoryConnection([]);

        var written = await ServeLoggedInAsync("EPSV\r\nRETR dir/../a.txt\r\n", TestContext.CancellationToken, PassiveDataConnections(dataConnection));

        Assert.AreEqual(PassiveReply + "150 Opening data connection for dir/../a.txt (12 bytes)\r\n" + TransferComplete, written);
        Assert.AreEqual(FileText, Text(dataConnection.WrittenBytes));
    }

    [TestMethod]
    public async Task Retr_RestPastTheEnd_Answers554AndClearsTheOffset()
    {
        var dataConnection = new InMemoryConnection([]);
        var dataConnections = PassiveDataConnections(dataConnection);

        var written = await ServeLoggedInAsync("EPSV\r\nREST 13\r\nRETR a.txt\r\nRETR a.txt\r\n", TestContext.CancellationToken, dataConnections);

        Assert.AreEqual(
            PassiveReply + "350 Restarting at 13\r\n554 Restart offset past end of file\r\n" + WholeFileOpening + TransferComplete,
            written);
        Assert.AreEqual(FileText, Text(dataConnection.WrittenBytes));
    }

    [TestMethod]
    public async Task Retr_RestAtTheEnd_SendsNoBytesAndCompletesTheDataConnection()
    {
        var dataConnection = new InMemoryConnection([]);

        var written = await ServeLoggedInAsync("EPSV\r\nREST 12\r\nRETR a.txt\r\n", TestContext.CancellationToken, PassiveDataConnections(dataConnection));

        Assert.AreEqual(
            PassiveReply + "350 Restarting at 12\r\n150 Opening data connection for a.txt (0 bytes)\r\n" + TransferComplete,
            written);
        Assert.IsEmpty(dataConnection.WrittenBytes);
        Assert.IsTrue(dataConnection.WritesCompleted);
    }

    [TestMethod]
    public async Task Retr_AfterATransfer_NeedsANewDataConnectionAndNoLongerRestarts()
    {
        var first = new InMemoryConnection([]);
        var second = new InMemoryConnection([]);
        var dataConnections = PassiveDataConnections(first).ScriptPassiveListener(PassiveEndPoint, second);

        var written = await ServeLoggedInAsync(
            "EPSV\r\nREST 5\r\nRETR a.txt\r\nRETR a.txt\r\nEPSV\r\nRETR a.txt\r\n", TestContext.CancellationToken, dataConnections);

        Assert.AreEqual(
            PassiveReply + "350 Restarting at 5\r\n150 Opening data connection for a.txt (7 bytes)\r\n" + TransferComplete
            + "425 Use PASV or PORT first\r\n"
            + PassiveReply + WholeFileOpening + TransferComplete,
            written);
        Assert.AreEqual(FileText[5..], Text(first.WrittenBytes));
        Assert.AreEqual(FileText, Text(second.WrittenBytes));
    }

    [TestMethod]
    public async Task Retr_PassiveAcceptTimesOut_Answers425AndNotesIt()
    {
        var dataConnections = new InMemoryDataConnections().ScriptPassiveListener(PassiveEndPoint, null);
        var log = new RecordingExchangeLog();

        var written = await ServeLoggedInAsync("EPSV\r\nRETR a.txt\r\n", TestContext.CancellationToken, dataConnections, log);

        Assert.AreEqual(PassiveReply + "425 Cannot open data connection\r\n", written);
        var listener = dataConnections.PassiveListeners.Single();
        CollectionAssert.AreEqual(new[] { ExchangeLimits.Default.HeadTimeout }, listener.AcceptTimeouts.ToArray());
        Assert.IsTrue(listener.Disposed);
        CollectionAssert.AreEqual(new[] { "No data connection was opened (TimedOut); answered 425." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task Retr_ActiveConnectFails_Answers425AndNotesIt()
    {
        var dataConnections = new InMemoryDataConnections().ScriptActiveFailure(DataConnectionFailure.Unreachable);
        var log = new RecordingExchangeLog();

        var written = await ServeLoggedInAsync("EPRT |1|127.0.0.1|50200|\r\nRETR a.txt\r\n", TestContext.CancellationToken, dataConnections, log);

        Assert.AreEqual("200 EPRT command successful\r\n425 Cannot open data connection\r\n", written);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 50200), dataConnections.ActiveRequests.Single().Target);
        CollectionAssert.AreEqual(new[] { "No data connection was opened (Unreachable); answered 425." }, log.Notes.ToArray());
    }

    [TestMethod]
    [DataRow("", "150 Opening data connection for a.txt (12 bytes)", DisplayName = "A write fails")]
    [DataRow("REST 12\r\n", "350 Restarting at 12\r\n150 Opening data connection for a.txt (0 bytes)", DisplayName = "Completing writes fails")]
    public async Task Retr_ClientClosesTheDataConnectionEarly_Answers426ThenAbor226(string restart, string replies)
    {
        var dataConnection = new InMemoryConnection([]);
        dataConnection.Abort();
        var log = new RecordingExchangeLog();

        var written = await ServeLoggedInAsync(
            $"EPSV\r\n{restart}RETR a.txt\r\nABOR\r\n", TestContext.CancellationToken, PassiveDataConnections(dataConnection), log);

        Assert.AreEqual(PassiveReply + replies + "\r\n426 Connection closed; transfer aborted\r\n226 Abort successful\r\n", written);
        Assert.IsTrue(dataConnection.Disposed);
        CollectionAssert.AreEqual(
            new[] { "The client closed the data connection before the whole file was sent; the data connection was reset." },
            log.Notes.ToArray());
    }

    [TestMethod]
    [DataRow(true, DisplayName = "IOException")]
    [DataRow(false, DisplayName = "UnauthorizedAccessException")]
    public async Task Retr_FileUnreadableAfter150_Answers451ResetsTheDataConnectionAndNotesWhy(bool isIOException)
    {
        Exception failure = isIOException ? new IOException("disk fault") : new UnauthorizedAccessException("denied");
        var fileSystem = new UnitTestThrowingContentFileSystem(failure, failsStatus: false);
        var dataConnection = new InMemoryConnection([]);
        var log = new RecordingExchangeLog();

        var written = await ServeLoggedInAsync(
            "EPSV\r\nRETR a.txt\r\n", TestContext.CancellationToken, PassiveDataConnections(dataConnection), log, fileSystem.ContentStore());

        Assert.AreEqual(PassiveReply + WholeFileOpening + "451 Cannot read the file\r\n", written);
        Assert.IsTrue(dataConnection.Aborted);
        Assert.IsFalse(dataConnection.WritesCompleted);
        var note = log.Notes.Single();
        StringAssert.EndsWith(note, $"a.txt could not be read after 150 was sent ({failure.GetType().Name}: {failure.Message}); the data connection was reset.");
    }

    [TestMethod]
    [DataRow("SIZE a.txt")]
    [DataRow("MDTM a.txt")]
    [DataRow("RETR a.txt")]
    public async Task Command_FileStatusUnreadable_Answers550AsForAMissingFileAndNotesWhy(string command)
    {
        var fileSystem = new UnitTestThrowingContentFileSystem(new IOException("disk fault"), failsStatus: true);
        var log = new RecordingExchangeLog();

        var written = await ServeLoggedInAsync(command + "\r\n", TestContext.CancellationToken, log: log, contentStore: fileSystem.ContentStore());

        Assert.AreEqual(NoSuchFile, written);
        StringAssert.EndsWith(log.Notes.Single(), "a.txt could not be read (IOException: disk fault); answered 550.");
    }

    [TestMethod]
    [DataRow(true, DisplayName = "Reading the status")]
    [DataRow(false, DisplayName = "Reading the file")]
    public async Task Retr_FileSystemThrowsSomethingElse_LetsTheDefectEscape(bool failsStatus)
    {
        var fileSystem = new UnitTestThrowingContentFileSystem(new InvalidOperationException("defect"), failsStatus);
        var control = new InMemoryConnection(Ascii(AnonymousLogin + "EPSV\r\nRETR a.txt\r\n"));
        var context = Context(new ManualTimeProvider(), TestContext.CancellationToken, dataConnections: PassiveDataConnections(new InMemoryConnection([])));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Server(contentStore: fileSystem.ContentStore()).ServeAsync(control, context));
    }

    private static InMemoryDataConnections PassiveDataConnections(InMemoryConnection dataConnection) =>
        new InMemoryDataConnections().ScriptPassiveListener(PassiveEndPoint, dataConnection);
}
