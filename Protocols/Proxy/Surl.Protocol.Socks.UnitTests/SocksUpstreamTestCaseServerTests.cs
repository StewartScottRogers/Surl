using Surl.Protocol.Abstractions;
using static Surl.Protocol.Socks.SocksTestKit;

namespace Surl.Protocol.Socks;

[TestClass]
public sealed class SocksUpstreamTestCaseServerTests
{
    private static readonly byte[] Socks5NoAuthGreeting = [0x05, 0x01, 0x00];

    private static readonly byte[] Socks5PasswordGreeting = [0x05, 0x01, 0x02];

    private static readonly byte[] Socks5MethodNone = [0x05, 0x00];

    private static readonly byte[] Socks5MethodPassword = [0x05, 0x02];

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        var replies = new SocksUpstreamTestCaseReplies([]);

        Assert.ThrowsExactly<ArgumentNullException>(() => new SocksUpstreamTestCaseServer(null!, new InMemoryOriginConnector()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SocksUpstreamTestCaseServer(replies, null!));
    }

    [TestMethod]
    public void Schemes_AreTheSocksServersSchemes()
    {
        var server = new SocksUpstreamTestCaseServer(new SocksUpstreamTestCaseReplies([]), new InMemoryOriginConnector());

        CollectionAssert.AreEqual(new[] { "socks4", "socks4a", "socks5", "socks5h" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_Socks4RequestForAnyHost_ConnectsToLoopbackAtTheRequestsPort()
    {
        var origins = new InMemoryOriginConnector().ScriptConnection("127.0.0.1", 8080, ConnectionOf());
        var client = ConnectionOf(Socks4Request(0x01, 8080, [9, 9, 9, 9], string.Empty));

        await ServeAsync([], origins, client, "socks4");

        CollectionAssert.AreEqual(Granted, client.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_BackendPort_ConnectsThereInsteadOfTheRequestsPort()
    {
        var origins = new InMemoryOriginConnector().ScriptConnection("127.0.0.1", 8080, ConnectionOf());
        var client = ConnectionOf(Socks5NoAuthGreeting, Socks5DomainRequest("this.is.a.host.name", 1));

        await ServeAsync(["backendport 8080"], origins, client, "socks5");

        CollectionAssert.AreEqual(Socks5MethodNone, client.WrittenBytes[..2]);
        Assert.AreEqual((byte)0x00, client.WrittenBytes[3], "The request was granted.");
        Assert.AreEqual(8080, origins.Requests.Single().Port);
    }

    [TestMethod]
    public async Task ServeAsync_Socks4Response_AnswersRejected()
    {
        var client = ConnectionOf(Socks4Request(0x01, 80, [127, 0, 0, 1], string.Empty));

        await ServeAsync(["response 91"], new InMemoryOriginConnector(), client, "socks4");

        CollectionAssert.AreEqual(Rejected, client.WrittenBytes);
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(2, 2)]
    [DataRow(3, 3)]
    [DataRow(4, 4)]
    [DataRow(5, 5)]
    [DataRow(6, 6)]
    [DataRow(91, 1)]
    public async Task ServeAsync_Socks5Response_AnswersThatCodeOrGeneralFailure(int response, int code)
    {
        var client = ConnectionOf(Socks5NoAuthGreeting, Socks5DomainRequest("origin.test", 80));

        await ServeAsync([$"response {response}"], new InMemoryOriginConnector(), client, "socks5");

        CollectionAssert.AreEqual(Cat(Socks5MethodNone, Socks5Failure((byte)code)), client.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_LoginRequiredAndNamedAccountGiven_AcceptsItAndConnects()
    {
        var origins = new InMemoryOriginConnector().ScriptConnection("127.0.0.1", 8080, ConnectionOf());
        var client = ConnectionOf(Socks5PasswordGreeting, LoginOf("uz3r", "p4ssworm"), Socks5DomainRequest("origin.test", 8080));

        await ServeAsync(["method 2", "user uz3r", "password p4ssworm"], origins, client, "socks5");

        CollectionAssert.AreEqual(Cat(Socks5MethodPassword, [0x01, 0x00]), client.WrittenBytes[..4]);
        Assert.AreEqual(1, origins.Requests.Count);
    }

    [TestMethod]
    public async Task ServeAsync_LoginRequiredAndOtherPasswordGiven_RefusesTheLogin()
    {
        var client = ConnectionOf(Socks5PasswordGreeting, LoginOf("uz3r", "wrong"));

        await ServeAsync(["method 2", "user uz3r", "password p4ssworm"], new InMemoryOriginConnector(), client, "socks5");

        CollectionAssert.AreEqual(Cat(Socks5MethodPassword, [0x01, 0x01]), client.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_LoginRequiredWithNoNamedAccount_AcceptsAnyLogin()
    {
        var origins = new InMemoryOriginConnector().ScriptConnection("127.0.0.1", 80, ConnectionOf());
        var client = ConnectionOf(Socks5PasswordGreeting, LoginOf("anyone", "anything"), Socks5DomainRequest("origin.test", 80));

        await ServeAsync(["method 2"], origins, client, "socks5");

        CollectionAssert.AreEqual(Cat(Socks5MethodPassword, [0x01, 0x00]), client.WrittenBytes[..4]);
    }

    [TestMethod]
    public async Task ServeAsync_LoginRequiredAndOnlyNoAuthOffered_AnswersNoAcceptableMethod()
    {
        var client = ConnectionOf(Socks5NoAuthGreeting);

        await ServeAsync(["method 2"], new InMemoryOriginConnector(), client, "socks5");

        CollectionAssert.AreEqual(SocksExchange.Socks5NoAcceptableMethod.ToArray(), client.WrittenBytes);
    }

    [TestMethod]
    public void Accounts_UserNamedWithNoPassword_AcceptsAnEmptyPassword()
    {
        var accounts = new SocksUpstreamTestCaseAccounts("bob", null);

        var verdict = accounts.CheckPasswordLoginAsync(new PasswordLogin("socks5", "bob", ReadOnlyMemory<byte>.Empty, null), CancellationToken.None);

        Assert.AreEqual(PasswordLoginVerdict.Accepted, verdict.Result);
    }

    [TestMethod]
    public void Accounts_UserNamedAndNoPasswordSent_RefusesTheCredentials()
    {
        var accounts = new SocksUpstreamTestCaseAccounts("bob", "pw");

        var verdict = accounts.CheckPasswordLoginAsync(new PasswordLogin("socks5", "bob", null, null), CancellationToken.None);

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, verdict.Result);
    }

    [TestMethod]
    public void Accounts_NullLogin_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SocksUpstreamTestCaseAccounts(null, null).CheckPasswordLoginAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public void Accounts_StartHttpConnection_IsNotSupported()
    {
        Assert.ThrowsExactly<NotSupportedException>(() => new SocksUpstreamTestCaseAccounts(null, null).StartHttpConnection(null));
    }

    [TestMethod]
    [DataRow(2, OriginConnectionFailure.NotAllowed)]
    [DataRow(3, OriginConnectionFailure.Unreachable)]
    [DataRow(4, OriginConnectionFailure.HostNotFound)]
    [DataRow(5, OriginConnectionFailure.Refused)]
    [DataRow(6, OriginConnectionFailure.TimedOut)]
    [DataRow(1, (OriginConnectionFailure)(-1))]
    public void FailureFor_Code_IsTheFailureThatMakesSocks5AnswerIt(int code, OriginConnectionFailure expected)
    {
        Assert.AreEqual(expected, SocksUpstreamTestCaseOriginConnector.FailureFor(code));
    }

    private static byte[] Cat(byte[] first, byte[] second) => [.. first, .. second];

    private static byte[] LoginOf(string user, string password) =>
        [0x01, (byte)user.Length, .. Latin1Bytes(user), (byte)password.Length, .. Latin1Bytes(password)];

    private static Task ServeAsync(string[] commands, InMemoryOriginConnector origins, IConnection client, string scheme) =>
        new SocksUpstreamTestCaseServer(new SocksUpstreamTestCaseReplies(commands), origins)
            .ServeAsync(client, Context(new RecordingExchangeLog(), scheme: scheme));
}
