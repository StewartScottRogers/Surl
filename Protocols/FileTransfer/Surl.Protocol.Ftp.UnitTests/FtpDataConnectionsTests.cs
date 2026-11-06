using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ftp.FtpTestExchange;

namespace Surl.Protocol.Ftp;

/// <summary>
/// <c>EPSV</c>, <c>PASV</c>, <c>EPRT</c> and <c>PORT</c>, and the defences on them (ADR-0052,
/// decision 6), with <see cref="InMemoryDataConnections"/> standing in for the seam.
/// </summary>
[TestClass]
public sealed class FtpDataConnectionsTests
{
    private const string ExtendedPassiveReply = "229 Entering Extended Passive Mode (|||50100|)\r\n";
    private const string NotYourAddress = "501 Address must be your own, port 1024 or above\r\n";
    private const string SyntaxError = "501 Syntax error in arguments\r\n";

    private static readonly IPEndPoint PassiveEndPoint = new(IPAddress.Loopback, 50100);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("EPSV")]
    [DataRow("EPSV 1")]
    [DataRow("EPSV 2")]
    public async Task Epsv_StartsAListenerForTheControlConnectionsAddresses(string command)
    {
        var dataConnections = new InMemoryDataConnections().ScriptPassiveListener(PassiveEndPoint, null);

        var written = await ServeLoggedInAsync(command + "\r\n", TestContext.CancellationToken, dataConnections);

        Assert.AreEqual(ExtendedPassiveReply, written);
        var request = dataConnections.PassiveRequests.Single();
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 80), request.ControlLocal);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 50000), request.ControlRemote);
        Assert.IsTrue(dataConnections.PassiveListeners.Single().Disposed, "The exchange's end disposes a listener still waiting.");
    }

    [TestMethod]
    public async Task Pasv_NamesTheControlConnectionsLocalAddressAndTheListenersPort()
    {
        var dataConnections = new InMemoryDataConnections().ScriptPassiveListener(PassiveEndPoint, null);

        var written = await ServeLoggedInAsync("PASV\r\n", TestContext.CancellationToken, dataConnections);

        Assert.AreEqual("227 Entering Passive Mode (127,0,0,1,195,180)\r\n", written);
    }

    [TestMethod]
    public async Task Pasv_OnAnIPv4MappedControlConnection_NamesTheIPv4Address()
    {
        var local = new IPEndPoint(IPAddress.Parse("::ffff:10.0.0.5"), 21);

        var written = await ServeWithLocalEndPointAsync(local, "PASV\r\n");

        Assert.AreEqual("227 Entering Passive Mode (10,0,0,5,195,180)\r\n", written);
    }

    [TestMethod]
    public async Task Pasv_OnAnIPv6ControlConnection_Answers425()
    {
        var written = await ServeWithLocalEndPointAsync(new IPEndPoint(IPAddress.IPv6Loopback, 21), "PASV\r\n");

        Assert.AreEqual("425 Use EPSV on IPv6\r\n", written);
    }

    [TestMethod]
    public async Task Pasv_OnAControlConnectionWithNoIPAddress_Answers425()
    {
        var written = await ServeWithLocalEndPointAsync(new DnsEndPoint("localhost", 21), "PASV\r\n");

        Assert.AreEqual("425 Use EPSV on IPv6\r\n", written);
    }

    [TestMethod]
    [DataRow("EPSV")]
    [DataRow("PASV")]
    public async Task PassiveCommand_SeamRefuses_Answers425AndNotesIt(string command)
    {
        var dataConnections = new InMemoryDataConnections().ScriptPassiveFailure(DataConnectionFailure.Unavailable);
        var log = new RecordingExchangeLog();

        var written = await ServeLoggedInAsync(command + "\r\n", TestContext.CancellationToken, dataConnections, log);

        Assert.AreEqual("425 Cannot open data connection\r\n", written);
        CollectionAssert.AreEqual(new[] { "No passive data listener was started (Unavailable); answered 425." }, log.Notes.ToArray());
    }

    [TestMethod]
    [DataRow("EPSV 3", "522 Network protocol not supported, use (1,2)")]
    [DataRow("EPRT |3|127.0.0.1|50200|", "522 Network protocol not supported, use (1,2)")]
    [DataRow("EPRT", "501 Syntax error in arguments")]
    [DataRow("PORT", "501 Syntax error in arguments")]
    [DataRow("EPRT |1|127.0.0.1|50200", "501 Syntax error in arguments")]
    [DataRow("EPRT x|1|127.0.0.1|50200|", "501 Syntax error in arguments")]
    [DataRow("EPRT |1|127.0.0.1||", "501 Syntax error in arguments")]
    [DataRow("EPRT |1|127.0.0.1|70000|", "501 Syntax error in arguments")]
    [DataRow("EPRT |1|127.0.0.1|+5000|", "501 Syntax error in arguments")]
    [DataRow("EPRT |1|::1|50200|", "501 Syntax error in arguments")]
    [DataRow("EPRT |1|nonsense|50200|", "501 Syntax error in arguments")]
    [DataRow("PORT 127,0,0,1,195", "501 Syntax error in arguments")]
    [DataRow("PORT 127,0,0,1,195,256", "501 Syntax error in arguments")]
    [DataRow("PORT 127,0,0,1,195,-1", "501 Syntax error in arguments")]
    [DataRow("PORT 127,0,0,1,195, 1", "501 Syntax error in arguments")]
    [DataRow("EPRT |1|10.0.0.9|50200|", "501 Address must be your own, port 1024 or above")]
    [DataRow("EPRT |2|::1|50200|", "501 Address must be your own, port 1024 or above")]
    [DataRow("EPRT |1|127.0.0.1|1023|", "501 Address must be your own, port 1024 or above")]
    [DataRow("PORT 10,0,0,9,195,200", "501 Address must be your own, port 1024 or above")]
    [DataRow("PORT 127,0,0,1,3,255", "501 Address must be your own, port 1024 or above")]
    public async Task ActiveCommand_Refused_AnswersWhyAndDialsNothing(string command, string reply)
    {
        var dataConnections = new InMemoryDataConnections();

        var written = await ServeLoggedInAsync(command + "\r\nRETR a.txt\r\n", TestContext.CancellationToken, dataConnections);

        Assert.AreEqual(reply + "\r\n425 Use PASV or PORT first\r\n", written);
        Assert.IsEmpty(dataConnections.ActiveRequests);
        Assert.IsEmpty(dataConnections.PassiveRequests);
    }

    [TestMethod]
    [DataRow("EPRT |1|127.0.0.1|1024|", "200 EPRT command successful", 1024)]
    [DataRow("EPRT !2!::ffff:127.0.0.1!50200!", "200 EPRT command successful", 50200)]
    [DataRow("PORT 127,0,0,1,195,200", "200 PORT command successful", 50120)]
    public async Task ActiveCommand_ForThePeersAddress_DialsItForTheTransfer(string command, string reply, int port)
    {
        var dataConnection = new InMemoryConnection([]);
        var dataConnections = new InMemoryDataConnections().ScriptActiveConnection(dataConnection);

        var written = await ServeLoggedInAsync(command + "\r\nRETR a.txt\r\n", TestContext.CancellationToken, dataConnections);

        Assert.AreEqual(reply + "\r\n150 Opening data connection for a.txt (12 bytes)\r\n226 Transfer complete\r\n", written);
        Assert.AreEqual(port, dataConnections.ActiveRequests.Single().Target.Port);
        Assert.AreEqual(FileText, Text(dataConnection.WrittenBytes));
    }

    [TestMethod]
    public async Task ActiveCommand_OnAControlConnectionWithNoIPPeer_IsRefused()
    {
        var control = new InMemoryConnection(Ascii(AnonymousLogin + "EPRT |1|127.0.0.1|50200|\r\n"), remoteEndPoint: new DnsEndPoint("localhost", 50000));

        await Server().ServeAsync(control, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        Assert.AreEqual(Greeting + AnonymousLoginReplies + NotYourAddress, Text(control.WrittenBytes));
    }

    [TestMethod]
    public async Task EpsvAll_AnswersOnlyEpsvFromThenOn()
    {
        var dataConnections = new InMemoryDataConnections().ScriptPassiveListener(PassiveEndPoint, null);

        var written = await ServeLoggedInAsync(
            "EPSV ALL\r\nPASV\r\nEPRT |1|127.0.0.1|50200|\r\nPORT 127,0,0,1,195,200\r\nepsv all\r\nEPSV\r\n", TestContext.CancellationToken, dataConnections);

        const string OnlyEpsv = "503 Only EPSV after EPSV ALL\r\n";
        Assert.AreEqual("200 EPSV ALL accepted\r\n" + OnlyEpsv + OnlyEpsv + OnlyEpsv + "200 EPSV ALL accepted\r\n" + ExtendedPassiveReply, written);
        Assert.IsEmpty(dataConnections.ActiveRequests);
    }

    [TestMethod]
    public async Task Epsv_Twice_DisposesTheFirstListenerAndAcceptsOnTheSecond()
    {
        var dataConnection = new InMemoryConnection([]);
        var dataConnections = new InMemoryDataConnections()
            .ScriptPassiveListener(PassiveEndPoint, null)
            .ScriptPassiveListener(PassiveEndPoint, dataConnection);

        await ServeLoggedInAsync("EPSV\r\nEPSV\r\nRETR a.txt\r\n", TestContext.CancellationToken, dataConnections);

        var first = dataConnections.PassiveListeners[0];
        Assert.IsTrue(first.Disposed);
        Assert.IsEmpty(first.AcceptTimeouts);
        Assert.AreEqual(FileText, Text(dataConnection.WrittenBytes));
    }

    [TestMethod]
    public async Task Eprt_AfterEpsv_DisposesTheListenerAndDials()
    {
        var dataConnection = new InMemoryConnection([]);
        var dataConnections = new InMemoryDataConnections()
            .ScriptPassiveListener(PassiveEndPoint, null)
            .ScriptActiveConnection(dataConnection);

        await ServeLoggedInAsync("EPSV\r\nEPRT |1|127.0.0.1|50200|\r\nRETR a.txt\r\n", TestContext.CancellationToken, dataConnections);

        Assert.IsTrue(dataConnections.PassiveListeners.Single().Disposed);
        Assert.IsEmpty(dataConnections.PassiveListeners.Single().AcceptTimeouts);
        Assert.AreEqual(FileText, Text(dataConnection.WrittenBytes));
    }

    [TestMethod]
    public async Task Epsv_AfterEprt_AcceptsInsteadOfDialling()
    {
        var dataConnection = new InMemoryConnection([]);
        var dataConnections = new InMemoryDataConnections().ScriptPassiveListener(PassiveEndPoint, dataConnection);

        await ServeLoggedInAsync("EPRT |1|127.0.0.1|50200|\r\nEPSV\r\nRETR a.txt\r\n", TestContext.CancellationToken, dataConnections);

        Assert.IsEmpty(dataConnections.ActiveRequests);
        Assert.AreEqual(FileText, Text(dataConnection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("EPSV")]
    [DataRow("PASV")]
    [DataRow("EPRT |1|127.0.0.1|50200|")]
    [DataRow("PORT 127,0,0,1,195,200")]
    [DataRow("SIZE a.txt")]
    [DataRow("RETR a.txt")]
    public async Task DataCommand_BeforeLogin_Answers530(string command)
    {
        var dataConnections = new InMemoryDataConnections();

        var written = await ServeAsync(command + "\r\n", TestContext.CancellationToken, dataConnections: dataConnections);

        Assert.AreEqual(Greeting + "530 Please log in with USER and PASS\r\n", written);
        Assert.IsEmpty(dataConnections.PassiveRequests);
    }

    private async Task<string> ServeWithLocalEndPointAsync(EndPoint localEndPoint, string commands)
    {
        var dataConnections = new InMemoryDataConnections().ScriptPassiveListener(PassiveEndPoint, null);
        var control = new InMemoryConnection([Encoding.ASCII.GetBytes(AnonymousLogin + commands)], localEndPoint: localEndPoint);

        await Server().ServeAsync(control, Context(new ManualTimeProvider(), TestContext.CancellationToken, dataConnections: dataConnections));

        return Text(control.WrittenBytes)[(Greeting + AnonymousLoginReplies).Length..];
    }
}
