using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ftp.FtpTestExchange;

namespace Surl.Protocol.Ftp;

/// <summary>
/// The FTP server half of upstream test cases (ADR-0080 decision 2), driven with inline cases
/// through <see cref="InMemoryConnection"/> and <see cref="InMemoryDataConnections"/>: Surl's own
/// replies, the case's content and <c>&lt;servercmd&gt;</c> lines, and the bytes it records.
/// </summary>
[TestClass]
public sealed class FtpUpstreamTestCaseServerTests
{
    private const string PassiveReply = "229 Entering Extended Passive Mode (|||50100|)\r\n";
    private const string TransferComplete = "226 Transfer complete\r\n";
    private const string Data = "0123456789abcdef\n";

    private static readonly IPEndPoint PassiveEndPoint = new(IPAddress.Loopback, 50100);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task NoServerCommands_GreetsAsSurlAnswersWithoutALoginAndRecordsEveryByte()
    {
        const string commands = "PWD\r\nUSER anonymous\r\nPASS ftp@example.com\r\nQUIT\r\n";
        var replies = Replies([]);

        var (written, _) = await ServeAsync(replies, commands);

        Assert.AreEqual(
            Greeting + "257 \"/\" is the current directory\r\n331 Password required\r\n230 Logged in\r\n221 Goodbye\r\n",
            written);
        Assert.AreEqual(commands, Latin1(replies.ReceivedBytes));
    }

    [TestMethod]
    [DataRow("REPLY welcome 230 Welcome", "", "230 Welcome\r\n", DisplayName = "REPLY welcome replaces the greeting")]
    [DataRow("REPLY EPSV 500 no such command", "EPSV\r\n", Greeting + "500 no such command\r\n", DisplayName = "REPLY answers in place of the command")]
    [DataRow("REPLY pwd 257 \"/\"", "PWD\r\n", Greeting + "257 \"/\"\r\n", DisplayName = "REPLY's command matches in any case")]
    [DataRow("REPLYLF PWD 257 \"/\"", "PWD\r\n", Greeting + "257 \"/\"\n", DisplayName = "REPLYLF ends the answer with LF")]
    [DataRow("REPLY AUTH 500 one\\r\\n331 two", "AUTH SSL\r\n", Greeting + "500 one\r\n331 two\r\n", DisplayName = "REPLY reads Perl escapes")]
    [DataRow("REPLY PASS 230 logged\\x00 in", "PASS x\r\n", Greeting + "230 logged\0 in\r\n", DisplayName = "REPLY sends any byte")]
    [DataRow("REPLY SYST 215  OS/400 runs this server", "SYST\r\n", Greeting + "215  OS/400 runs this server\r\n", DisplayName = "REPLY keeps the response's spaces")]
    public async Task Reply_IsSentInPlaceOfTheServersAnswer(string serverCommand, string commands, string expected)
    {
        var (written, _) = await ServeAsync(Replies([serverCommand]), commands);

        Assert.AreEqual(expected, written);
    }

    [TestMethod]
    public async Task Reply_EpsvIsNotRun_NoPassiveListenerIsStarted()
    {
        var dataConnections = Passive(new InMemoryConnection([]));

        await ServeAsync(Replies(["REPLY EPSV 500 no such command"]), "EPSV\r\n", dataConnections);

        Assert.IsEmpty(dataConnections.PassiveRequests);
    }

    [TestMethod]
    [DataRow("1", "550 nope\r\n250 Directory changed\r\n250 Directory changed\r\n", DisplayName = "COUNT 1")]
    [DataRow("2", "550 nope\r\n550 nope\r\n250 Directory changed\r\n", DisplayName = "COUNT 2")]
    public async Task Count_KeepsTheReplyForThatManyAnswers(string count, string expected)
    {
        var (written, _) = await ServeAsync(Replies(["REPLY CWD 550 nope", $"COUNT CWD {count}"]), "CWD a\r\nCWD b\r\nCWD c\r\n");

        Assert.AreEqual(Greeting + expected, written);
    }

    [TestMethod]
    [DataRow("CWD any/where", "250 Directory changed", DisplayName = "CWD enters any directory")]
    [DataRow("XCWD test-1", "250 Directory changed", DisplayName = "XCWD enters any directory")]
    [DataRow("CWD", "501 Syntax error in arguments", DisplayName = "CWD with no directory")]
    [DataRow("DELE file", "250 File deleted", DisplayName = "DELE deletes any file")]
    [DataRow("DELE", "501 Syntax error in arguments", DisplayName = "DELE with no file")]
    [DataRow("SIZE 135", "213 17", DisplayName = "SIZE with no size part is data's length")]
    [DataRow("SIZE", "501 Syntax error in arguments", DisplayName = "SIZE with no file")]
    [DataRow("MDTM 135", "550 No such file", DisplayName = "MDTM with no mdtm part")]
    [DataRow("MDTM", "501 Syntax error in arguments", DisplayName = "MDTM with no file")]
    [DataRow("RETR", "501 Syntax error in arguments", DisplayName = "RETR with no file")]
    [DataRow("RETR 135", "425 Use PASV or PORT first", DisplayName = "RETR with no data connection")]
    [DataRow("STOR", "501 Syntax error in arguments", DisplayName = "STOR with no file")]
    [DataRow("APPE", "501 Syntax error in arguments", DisplayName = "APPE with no file")]
    public async Task Command_WithTheCasesData_IsAnsweredWithoutADataConnection(string command, string reply)
    {
        var (written, _) = await ServeAsync(Replies([], ("data", Data)), command + "\r\n");

        Assert.AreEqual(Greeting + reply + "\r\n", written);
    }

    [TestMethod]
    [DataRow("size", "64\n", "SIZE 135", "213 64", DisplayName = "SIZE answers the size part")]
    [DataRow("size", "-1\n", "SIZE 135", "550 No such file", DisplayName = "SIZE -1 is a missing file")]
    [DataRow("mdtm", "213 20030409102659\n", "MDTM 135", "213 20030409102659", DisplayName = "MDTM answers the mdtm part as written")]
    [DataRow("mdtm", "-1\n", "MDTM 135", "550 No such file", DisplayName = "MDTM -1 is a missing file")]
    [DataRow("data2", Data, "SIZE 135", "550 No such file", DisplayName = "SIZE of a file the case lacks")]
    public async Task FileStatus_IsAnsweredFromTheCasesParts(string part, string text, string command, string reply)
    {
        var (written, _) = await ServeAsync(Replies([], (part, text)), command + "\r\n");

        Assert.AreEqual(Greeting + reply + "\r\n", written);
    }

    [TestMethod]
    [DataRow("RETR 135", "data", DisplayName = "A test number picks data")]
    [DataRow("RETR file.txt", "data", DisplayName = "A name with no digits picks data")]
    [DataRow("RETR 99999999999999999999999", "data", DisplayName = "A number too long to read picks data")]
    [DataRow("RETR 10350002", "data2", DisplayName = "A number over 10000 picks its remainder's part")]
    public async Task Retr_SendsThePartTheNamesNumberPicks(string command, string expectedPart)
    {
        var parts = new[] { ("data", Data), ("data2", "second\n") };
        var expected = parts.Single(part => part.Item1 == expectedPart).Item2;
        var dataConnection = new InMemoryConnection([]);
        var name = command["RETR ".Length..];

        var (written, _) = await ServeAsync(Replies([], parts), $"EPSV\r\n{command}\r\n", Passive(dataConnection));

        Assert.AreEqual(Greeting + PassiveReply + $"150 Opening data connection for {name} ({expected.Length} bytes)\r\n" + TransferComplete, written);
        Assert.AreEqual(expected, Latin1(dataConnection.WrittenBytes));
        Assert.IsTrue(dataConnection.WritesCompleted);
    }

    [TestMethod]
    public async Task Retr_AfterRest_SendsAllOfData()
    {
        var dataConnection = new InMemoryConnection([]);

        var (written, _) = await ServeAsync(Replies([], ("data", Data)), "EPSV\r\nREST 4\r\nRETR 135\r\n", Passive(dataConnection));

        Assert.AreEqual(Greeting + PassiveReply + "350 Restarting at 4\r\n150 Opening data connection for 135 (17 bytes)\r\n" + TransferComplete, written);
        Assert.AreEqual(Data, Latin1(dataConnection.WrittenBytes));
    }

    [TestMethod]
    public async Task Retr_FileTheCaseLacks_Answers550()
    {
        var (written, _) = await ServeAsync(Replies([]), "EPSV\r\nRETR 135\r\n", Passive(new InMemoryConnection([])));

        Assert.AreEqual(Greeting + PassiveReply + "550 No such file\r\n", written);
    }

    [TestMethod]
    [DataRow("RETRNOSIZE", "150 Opening data connection for 135", DisplayName = "RETRNOSIZE names no size")]
    [DataRow("RETRSIZE 7", "150 Opening data connection for 135 (7 bytes)", DisplayName = "RETRSIZE names its size")]
    public async Task Retr_SizeServerCommands_ChangeThe150(string serverCommand, string opening)
    {
        var (written, _) = await ServeAsync(Replies([serverCommand], ("data", Data)), "EPSV\r\nRETR 135\r\n", Passive(new InMemoryConnection([])));

        Assert.AreEqual(Greeting + PassiveReply + opening + "\r\n" + TransferComplete, written);
    }

    [TestMethod]
    public async Task Retr_ClientClosesTheDataConnection_Answers426()
    {
        var log = new RecordingExchangeLog();
        var dataConnection = new InMemoryConnection([]);
        dataConnection.Abort();

        var (written, _) = await ServeAsync(Replies([], ("data", Data)), "EPSV\r\nRETR 135\r\n", Passive(dataConnection), log);

        Assert.EndsWith("426 Connection closed; transfer aborted\r\n", written);
        Assert.Contains("before the whole file was sent", string.Join('\n', log.Notes));
    }

    [TestMethod]
    [DataRow("LIST")]
    [DataRow("NLST")]
    [DataRow("LIST -la test-135")]
    public async Task Listing_SendsData(string command)
    {
        var dataConnection = new InMemoryConnection([]);

        var (written, _) = await ServeAsync(Replies([], ("data", "total 0\r\n")), $"EPSV\r\n{command}\r\n", Passive(dataConnection));

        Assert.AreEqual(Greeting + PassiveReply + "150 Opening data connection for directory listing\r\n" + TransferComplete, written);
        Assert.AreEqual("total 0\r\n", Latin1(dataConnection.WrittenBytes));
    }

    [TestMethod]
    public async Task Listing_NoData_SendsAnEmptyListing()
    {
        var dataConnection = new InMemoryConnection([]);

        await ServeAsync(Replies([]), "EPSV\r\nLIST\r\n", Passive(dataConnection));

        Assert.IsEmpty(dataConnection.WrittenBytes);
        Assert.IsTrue(dataConnection.WritesCompleted);
    }

    [TestMethod]
    [DataRow("STOR", new string[0], "226 Transfer complete", "upload one\nupload two\n", DisplayName = "STOR keeps the upload")]
    [DataRow("APPE", new string[0], "226 Transfer complete", "upload one\nupload two\n", DisplayName = "APPE keeps the upload")]
    [DataRow("STOR", new[] { "STOR 552 disk full" }, "552 disk full", "upload one\nupload two\n", DisplayName = "STOR [msg] ends the upload")]
    [DataRow("STOR", new[] { "NOSAVE" }, "226 Transfer complete", "", DisplayName = "NOSAVE keeps nothing")]
    public async Task Upload_ReadsTheDataConnectionToItsEnd(string command, string[] serverCommands, string finalReply, string kept)
    {
        var replies = Replies(serverCommands);
        var dataConnection = new InMemoryConnection(Ascii("upload one\n", "upload two\n"));

        var (written, _) = await ServeAsync(replies, $"EPSV\r\n{command} 135\r\n", Passive(dataConnection));

        Assert.AreEqual(Greeting + PassiveReply + "150 Opening data connection for 135\r\n" + finalReply + "\r\n", written);
        Assert.AreEqual(kept, Latin1(replies.UploadedBytes));
    }

    [TestMethod]
    public async Task Upload_DataConnectionFails_Answers426AndResetsIt()
    {
        var replies = Replies([]);
        var log = new RecordingExchangeLog();
        var dataConnection = new ReadFailingConnection(Ascii("part"));

        var (written, _) = await ServeAsync(replies, "EPSV\r\nSTOR 135\r\n", Passive(dataConnection), log);

        Assert.EndsWith("150 Opening data connection for 135\r\n426 Connection closed; transfer aborted\r\n", written);
        Assert.AreEqual("part", Latin1(replies.UploadedBytes));
        Assert.IsTrue(dataConnection.Aborted);
        Assert.Contains("before the whole upload arrived", string.Join('\n', log.Notes));
    }

    [TestMethod]
    public void Schemes_AreFtpAndFtps()
    {
        var server = new FtpUpstreamTestCaseServer(Replies([]), new AnonymousAuthenticationPolicy());

        CollectionAssert.AreEqual(new[] { "ftp", "ftps" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task NullArguments_Throw()
    {
        var replies = Replies([]);
        var server = new FtpUpstreamTestCaseServer(replies, new AnonymousAuthenticationPolicy());

        Assert.ThrowsExactly<ArgumentNullException>(() => new FtpUpstreamTestCaseServer(null!, new AnonymousAuthenticationPolicy()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new FtpUpstreamTestCaseServer(replies, null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => server.ServeAsync(null!, Context(new ManualTimeProvider(), TestContext.CancellationToken)));
    }

    private static FtpUpstreamTestCaseReplies Replies(string[] serverCommands, params (string Name, string Text)[] parts) =>
        new(parts.ToDictionary(part => part.Name, part => Encoding.Latin1.GetBytes(part.Text)), serverCommands);

    private static InMemoryDataConnections Passive(IConnection dataConnection) =>
        new InMemoryDataConnections().ScriptPassiveListener(PassiveEndPoint, dataConnection);

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    private async Task<(string Written, InMemoryConnection Connection)> ServeAsync(
        FtpUpstreamTestCaseReplies replies, string commands, IDataConnectionOpener? dataConnections = null, IExchangeLog? log = null)
    {
        var connection = new InMemoryConnection(Ascii(commands));
        var context = Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log, dataConnections: dataConnections);

        await new FtpUpstreamTestCaseServer(replies, new AnonymousAuthenticationPolicy()).ServeAsync(connection, context);

        return (Latin1(connection.WrittenBytes), connection);
    }
}
