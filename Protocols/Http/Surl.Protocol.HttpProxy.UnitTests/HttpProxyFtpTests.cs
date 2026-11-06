using Surl.Protocol.Abstractions;
using static Surl.Protocol.HttpProxy.ProxyTestKit;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// Forwarding an <c>ftp://</c> absolute-form <c>GET</c> (ADR-0088 decision 3 and Amendment 4):
/// the requests upstream curl 8.21.0 sent (Fixtures/README.md) replayed against an in-memory FTP
/// origin, what the origin is sent, and what curl receives.
/// </summary>
[TestClass]
public sealed class HttpProxyFtpTests
{
    private const int DataPort = 5000;

    private const string Greeting = "220 ready\r\n";

    private const string LoggedIn = "331 password please\r\n230 logged in\r\n";

    private const string Extended = "229 Entering Extended Passive Mode (|||5000|)\r\n";

    private const string Opening = "150 opening\r\n";

    private const string Complete = "226 done\r\n";

    private const string FileHead = "HTTP/1.1 200 OK\r\n" + DateField + "Server: surl\r\nContent-Type: application/octet-stream\r\nTransfer-Encoding: chunked\r\nVia: 1.1 surl\r\n\r\n";

    private static string Get(string target, string fields = "") =>
        $"GET {target} HTTP/1.1\r\nHost: origin.test:21\r\n{fields}\r\n";

    private static string Commands(params string[] lines) => string.Concat(lines.Select(line => line + "\r\n"));

    private static InMemoryOriginConnector Origin(IConnection control, IConnection? data = null)
    {
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 21, control);

        return data is null ? origins : origins.ScriptConnection("origin.test", DataPort, data);
    }

    private static InMemoryConnection Control(params string[] replies) => Connection(replies);

    [TestMethod]
    public async Task ServeAsync_MeasuredFileRequest_FetchesTheFileAndAnswers200Chunked()
    {
        var control = Control(Greeting, LoggedIn, "200 binary\r\n", Extended, Opening, Complete);
        var data = Connection("hello\n");
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(ConnectionOf(Fixture("ftp-forward-file", "request.bin")), Origin(control, data), log);

        Assert.AreEqual(FixtureText("ftp-forward-file", "response.bin"), written);
        Assert.AreEqual(Commands("USER user", "PASS pass", "TYPE I", "EPSV", "RETR a.txt", "QUIT"), Latin1(control.WrittenBytes));
        Assert.IsTrue(control.Disposed);
        Assert.IsTrue(data.Disposed);
        CollectionAssert.Contains(log.Notes.ToList(), "Fetched RETR from FTP origin origin.test:21; answered 200.");
    }

    [TestMethod]
    public async Task ServeAsync_MeasuredListingRequest_ListsTheDirectoryAsText()
    {
        var control = Control(Greeting, LoggedIn, "200 binary\r\n", Extended, Opening, Complete);

        var written = await ServeAsync(ConnectionOf(Fixture("ftp-forward-listing", "request.bin")), Origin(control, Connection("a.txt\r\n")));

        Assert.AreEqual(FixtureText("ftp-forward-listing", "response.bin"), written);
        Assert.AreEqual(Commands("USER user", "PASS pass", "TYPE I", "EPSV", "LIST dir/", "QUIT"), Latin1(control.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_MeasuredRequestWhoseLoginTheOriginRefuses_Answers502()
    {
        var control = Control(Greeting, "331 password please\r\n530 login incorrect\r\n");
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(ConnectionOf(Fixture("ftp-forward-502", "request.bin")), Origin(control), log);

        Assert.AreEqual(FixtureText("ftp-forward-502", "response.bin"), written);
        Assert.AreEqual(Commands("USER anonymous", "PASS ftp@example.com"), Latin1(control.WrittenBytes));
        CollectionAssert.Contains(log.Notes.ToList(), "Answered 502: FTP origin origin.test:21 answered PASS with 530 login incorrect.");
        Assert.IsTrue(control.Disposed);
    }

    [TestMethod]
    public async Task ServeAsync_FtpOriginThatDoesNotAnswerInTime_Answers504()
    {
        var control = HeldOpenConnection();
        var clock = new ManualTimeProvider(Now);
        var client = ConnectionOf(Fixture("ftp-forward-502", "request.bin"));

        var serving = new HttpProxyProtocolServer(Origin(control)).ServeAsync(client, Context(new RecordingExchangeLog(), clock: clock));
        clock.Advance(ExchangeLimits.Default.HeadTimeout);
        await serving;

        Assert.AreEqual(Answer(504, "Gateway Timeout").Replace("12:00:00", "12:00:30"), Latin1(client.WrittenBytes));
        Assert.IsTrue(control.Disposed);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledWhileTheFtpOriginIsAwaited_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        var client = Connection(Get("ftp://origin.test/a.txt"));
        var serving = new HttpProxyProtocolServer(Origin(HeldOpenConnection())).ServeAsync(client, Context(new RecordingExchangeLog(), cancellationToken: cancellation.Token));

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    [DataRow(OriginConnectionFailure.NotAllowed, 403, "Forbidden")]
    [DataRow(OriginConnectionFailure.Refused, 502, "Bad Gateway")]
    [DataRow(OriginConnectionFailure.TimedOut, 504, "Gateway Timeout")]
    public async Task ServeAsync_FtpOriginThatCannotBeReached_AnswersDecisionTwosStatus(OriginConnectionFailure failure, int code, string reason)
    {
        var origins = new InMemoryOriginConnector().ScriptFailure("origin.test", 21, failure);

        var written = await ServeAsync(Connection(Get("ftp://origin.test/a.txt")), origins);

        Assert.AreEqual(Answer(code, reason), written);
    }

    [TestMethod]
    [DataRow(OriginConnectionFailure.NotAllowed, 502, "Bad Gateway")]
    [DataRow(OriginConnectionFailure.TimedOut, 504, "Gateway Timeout")]
    public async Task ServeAsync_DataPortThatCannotBeReached_Answers502Or504(OriginConnectionFailure failure, int code, string reason)
    {
        var control = Control(Greeting, LoggedIn, "200 binary\r\n", Extended);
        var origins = Origin(control).ScriptFailure("origin.test", DataPort, failure);
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(Connection(Get("ftp://origin.test/a.txt")), origins, log);

        Assert.AreEqual(Answer(code, reason), written);
        CollectionAssert.Contains(log.Notes.ToList(), $"Answered {code}: FTP origin origin.test:21 could not be reached on its data port 5000 ({failure}).");
    }

    [TestMethod]
    public async Task ServeAsync_FileTheOriginRefuses_Answers502AndClosesTheDataConnection()
    {
        var control = Control(Greeting, LoggedIn, "200 binary\r\n", Extended, "550 no such file\r\n");
        var data = HeldOpenConnection();

        var written = await ServeAsync(Connection(Get("ftp://origin.test/a.txt")), Origin(control, data));

        Assert.AreEqual(Answer(502, "Bad Gateway"), written);
        Assert.IsTrue(data.Disposed);
    }

    [TestMethod]
    public async Task ServeAsync_UserLoggedInWithoutAPassword_SendsNoPass()
    {
        var control = Control(Greeting, "230 welcome\r\n", "200 binary\r\n", Extended, Opening, Complete);

        var written = await ServeAsync(Connection(Get("ftp://origin.test/a.txt")), Origin(control, Connection("hello\n")));

        Assert.AreEqual(FileHead + "6\r\nhello\n\r\n0\r\n\r\n", written);
        Assert.AreEqual(Commands("USER anonymous", "TYPE I", "EPSV", "RETR a.txt", "QUIT"), Latin1(control.WrittenBytes));
    }

    [TestMethod]
    [DataRow("500 unknown command\r\n")]
    [DataRow("229 Entering Extended Passive Mode\r\n")]
    [DataRow("229 Entering Extended Passive Mode (|||0|)\r\n")]
    public async Task ServeAsync_ExtendedPassiveModeNotGiven_FallsBackToPasv(string epsvReply)
    {
        var control = Control(Greeting, LoggedIn, "200 binary\r\n", epsvReply, "227 Entering Passive Mode (127,0,0,1,19,136).\r\n", Opening, Complete);

        var written = await ServeAsync(Connection(Get("ftp://origin.test/a.txt")), Origin(control, Connection("hello\n")));

        Assert.AreEqual(FileHead + "6\r\nhello\n\r\n0\r\n\r\n", written);
        StringAssert.Contains(Latin1(control.WrittenBytes), "EPSV\r\nPASV\r\nRETR a.txt\r\n");
    }

    [TestMethod]
    [DataRow("500 no\r\n")]
    [DataRow("227 Entering Passive Mode\r\n")]
    [DataRow("227 Entering Passive Mode (127,0,0,1,19)\r\n")]
    [DataRow("227 Entering Passive Mode (127,0,0,1,300,1)\r\n")]
    [DataRow("227 Entering Passive Mode (127,0,0,1,19,300)\r\n")]
    [DataRow("227 Entering Passive Mode (127,0,0,1,0,0)\r\n")]
    public async Task ServeAsync_NoPassiveDataPort_Answers502(string pasvReply)
    {
        var control = Control(Greeting, LoggedIn, "200 binary\r\n", "500 no\r\n", pasvReply);
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(Connection(Get("ftp://origin.test/a.txt")), Origin(control), log);

        Assert.AreEqual(Answer(502, "Bad Gateway"), written);
        CollectionAssert.Contains(log.Notes.ToList(), $"Answered 502: FTP origin origin.test:21 answered EPSV and PASV with no data port ({pasvReply.TrimEnd()}).");
    }

    [TestMethod]
    [DataRow("hello\r\n", "answered the greeting with a line that is no FTP reply")]
    [DataRow("", "closed the control connection or sent too long a line during the greeting")]
    [DataRow("421 too busy\r\n", "answered the greeting with 421 too busy")]
    [DataRow("099 early\r\n", "answered the greeting with a line that is no FTP reply")]
    [DataRow("22\r\n", "answered the greeting with a line that is no FTP reply")]
    [DataRow("2200\r\n", "answered the greeting with a line that is no FTP reply")]
    [DataRow("220\r\n", "closed the control connection or sent too long a line during USER")]
    public async Task ServeAsync_GreetingThatIsNotReady_Answers502(string greeting, string why)
    {
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(Connection(Get("ftp://origin.test/a.txt")), Origin(Control(greeting)), log);

        Assert.AreEqual(Answer(502, "Bad Gateway"), written);
        CollectionAssert.Contains(log.Notes.ToList(), $"Answered 502: FTP origin origin.test:21 {why}.");
    }

    [TestMethod]
    public async Task ServeAsync_MultiLineGreeting_IsReadToItsLastLine()
    {
        var control = Control("220-Welcome\r\n to the origin\r\n220-\r\n220 ready\r\n", LoggedIn, "200 binary\r\n", Extended, Opening, Complete);

        var written = await ServeAsync(Connection(Get("ftp://origin.test/a.txt")), Origin(control, Connection("hello\n")));

        Assert.AreEqual(FileHead + "6\r\nhello\n\r\n0\r\n\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_ControlConnectionThatFailsOnAWrite_Answers502()
    {
        var control = new FailingConnection(Control(Greeting), writesBeforeFailure: 0);
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(Connection(Get("ftp://origin.test/a.txt")), Origin(control), log);

        Assert.AreEqual(Answer(502, "Bad Gateway"), written);
        CollectionAssert.Contains(log.Notes.ToList(), "Answered 502: FTP origin origin.test:21 failed during USER.");
    }

    [TestMethod]
    public async Task ServeAsync_ControlConnectionThatFailsOnQuit_StillAnswersTheTransfer()
    {
        var control = new FailingConnection(Control(Greeting, LoggedIn, "200 binary\r\n", Extended, Opening, Complete), writesBeforeFailure: 5);

        var written = await ServeAsync(Connection(Get("ftp://origin.test/a.txt")), Origin(control, Connection("hello\n")));

        Assert.AreEqual(FileHead + "6\r\nhello\n\r\n0\r\n\r\n", written);
    }

    [TestMethod]
    [DataRow("426 transfer aborted\r\n")]
    [DataRow("")]
    public async Task ServeAsync_TransferTheOriginDoesNotComplete_AbortsCurlsConnection(string completion)
    {
        var control = Control(Greeting, LoggedIn, "200 binary\r\n", Extended, Opening, completion);
        var client = Connection(Get("ftp://origin.test/a.txt"));
        var log = new RecordingExchangeLog();

        await ServeAsync(client, Origin(control, Connection("hel")), log);

        Assert.IsTrue(client.Aborted);
        Assert.AreEqual(FileHead + "3\r\nhel\r\n", Latin1(client.WrittenBytes));
        CollectionAssert.Contains(log.Notes.ToList(), "FTP origin origin.test:21 did not complete the transfer; curl's connection is aborted.");
    }

    [TestMethod]
    public async Task ServeAsync_DataConnectionThatFails_AbortsCurlsConnection()
    {
        var control = Control(Greeting, LoggedIn, "200 binary\r\n", Extended, Opening, Complete);
        var client = Connection(Get("ftp://origin.test/a.txt"));

        await ServeAsync(client, Origin(control, new FailingConnection(Connection("hel"), resetsInsteadOfClosing: true)));

        Assert.IsTrue(client.Aborted);
    }

    [TestMethod]
    public async Task ServeAsync_RequestThatClosesTheConnection_IsAnsweredCloseDelimited()
    {
        var control = Control(Greeting, LoggedIn, "200 binary\r\n", Extended, Opening, Complete);
        var client = Connection(Get("ftp://origin.test/a.txt", "Proxy-Connection: close\r\n"));

        var written = await ServeAsync(client, Origin(control, Connection("hello\n")));

        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\n" + DateField + "Server: surl\r\nContent-Type: application/octet-stream\r\nVia: 1.1 surl\r\nConnection: close\r\n\r\nhello\n",
            written);
        Assert.IsTrue(client.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_TwoFtpRequests_AreAnsweredOnOneCurlConnection()
    {
        var first = Control(Greeting, LoggedIn, "200 binary\r\n", Extended, Opening, Complete);
        var second = Control(Greeting, LoggedIn, "200 binary\r\n", Extended, Opening, Complete);
        var origins = Origin(first, Connection("hello\n")).ScriptConnection("origin.test", 21, second).ScriptConnection("origin.test", DataPort, Connection("hello\n"));

        var written = await ServeAsync(Connection(Get("ftp://origin.test/a.txt"), Get("ftp://origin.test/a.txt")), origins);

        var answer = FileHead + "6\r\nhello\n\r\n0\r\n\r\n";
        Assert.AreEqual(answer + answer, written);
    }

    [TestMethod]
    [DataRow("ftp://origin.test/a.txt;type=a", "TYPE A", "RETR a.txt", "application/octet-stream")]
    [DataRow("ftp://origin.test/dir;type=d", "TYPE I", "NLST dir", "text/plain")]
    [DataRow("ftp://origin.test/", "TYPE I", "LIST", "text/plain")]
    [DataRow("ftp://origin.test/dir/a%20b.txt", "TYPE I", "RETR dir/a b.txt", "application/octet-stream")]
    public async Task ServeAsync_PathAndType_ChooseTheTypeAndTheTransferCommand(string target, string type, string transfer, string contentType)
    {
        var control = Control(Greeting, LoggedIn, "200 ok\r\n", Extended, Opening, Complete);

        var written = await ServeAsync(Connection(Get(target)), Origin(control, Connection("x")));

        Assert.AreEqual(Commands("USER anonymous", "PASS ftp@example.com", type, "EPSV", transfer, "QUIT"), Latin1(control.WrittenBytes));
        StringAssert.Contains(written, $"Content-Type: {contentType}\r\n");
    }

    [TestMethod]
    [DataRow("ftp://origin.test/a%0d%0aDELE%20b")]
    [DataRow("ftp://us%0aer@origin.test/a")]
    public async Task ServeAsync_ControlCharacterInTheTarget_Answers400WithoutReachingTheOrigin(string target)
    {
        var origins = new InMemoryOriginConnector();

        var written = await ServeAsync(Connection(Get(target)), origins);

        Assert.AreEqual(Answer(400, "Bad Request"), written);
        Assert.IsEmpty(origins.Requests);
    }

    [TestMethod]
    [DataRow("ftp://origin.test/a", "Authorization: Bearer abc\r\n", "USER anonymous", "PASS ftp@example.com")]
    [DataRow("ftp://origin.test/a", "Authorization: Basic dXNlcg==\r\n", "USER user", "PASS ")]
    [DataRow("ftp://us%40er@origin.test/a", "", "USER us@er", "PASS ")]
    [DataRow("ftp://u:p%3Aq@origin.test/a", "Authorization: Basic eDp5\r\n", "USER u", "PASS p:q")]
    public async Task ServeAsync_Credentials_AreTheUserInfoThenBasicThenAnonymous(string target, string fields, string user, string pass)
    {
        var control = Control(Greeting, "331 password please\r\n530 no\r\n");

        await ServeAsync(Connection(Get(target, fields)), Origin(control));

        Assert.AreEqual(Commands(user, pass), Latin1(control.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_BasicCredentialsThatAreNotBase64_Answers400()
    {
        var written = await ServeAsync(Connection(Get("ftp://origin.test/a", "Authorization: Basic !!!\r\n")), new InMemoryOriginConnector());

        Assert.AreEqual(Answer(400, "Bad Request"), written);
    }

    [TestMethod]
    [DataRow("POST")]
    [DataRow("HEAD")]
    public async Task ServeAsync_FtpRequestOtherThanGet_Answers501(string method)
    {
        var written = await ServeAsync(Connection($"{method} ftp://origin.test/a.txt HTTP/1.1\r\nHost: origin.test:21\r\n\r\n"), new InMemoryOriginConnector());

        Assert.AreEqual(Answer(501, "Not Implemented"), written);
    }
}
