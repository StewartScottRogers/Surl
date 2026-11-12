using System.Net;
using System.Security.Authentication;
using System.Text;
using Surl.Authentication;
using Surl.Extended.Protocol.DnsQuery;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class DnsConnectionRoutingProtocolServerTests
{
    private const string ExampleGetParameter = "AAABAAABAAAAAAAAA3d3dwdleGFtcGxlA2NvbQAAAQAB";

    public TestContext TestContext { get; set; } = null!;

    private sealed class RecordingConnectionServer : IConnectionProtocolServer
    {
        public int Served { get; private set; }

        public IReadOnlyList<string> Schemes { get; } = ["dns"];

        public Task ServeAsync(IConnection connection, ExchangeContext context)
        {
            Served++;
            return Task.CompletedTask;
        }
    }

    private static TlsSession Session(string? applicationProtocol) =>
        new(SslProtocols.Tls13, default, applicationProtocol, null, null);

    private ExchangeContext ContextFor(ListenUrl listenUrl) =>
        new(1, listenUrl, new IPEndPoint(IPAddress.Loopback, 1), new IPEndPoint(IPAddress.Loopback, 2), new RecordingExchangeLog(), TimeProvider.System, TestContext.CancellationToken);

    private static ListenUrl DnsOverHttps => new ListenUrl("dns", "127.0.0.1", 443) { IsImplicitTls = true, IsDnsOverHttps = true };

    private static DnsConnectionRoutingProtocolServer Server(RecordingConnectionServer stream) => new(stream, new DnsZoneSet(), allowZoneTransfer: false);

    [TestMethod]
    public async Task ServeAsync_ListenUrlThatIsNotDnsOverHttps_GoesToTheStreamServer()
    {
        var stream = new RecordingConnectionServer();
        var server = Server(stream);
        await using var connection = new InMemoryConnection([]);

        await server.ServeAsync(connection, ContextFor(new ListenUrl("dns", "127.0.0.1", 853) { IsImplicitTls = true }));

        Assert.AreEqual(1, stream.Served);
        Assert.AreSame(stream, server.StreamServer);
        CollectionAssert.AreEqual(new[] { "dns" }, server.Schemes.ToArray());
    }

    [TestMethod]
    [DataRow("http/1.1")]
    [DataRow(null)]
    public async Task ServeAsync_DnsOverHttpsWithoutH2_AnswersTheGetAsHttp11(string? applicationProtocol)
    {
        var stream = new RecordingConnectionServer();
        var request = $"GET /dns-query?dns={ExampleGetParameter} HTTP/1.1\r\nHost: dnsserver.example.net\r\nAccept: application/dns-message\r\nConnection: close\r\n\r\n";
        await using var connection = new InMemoryConnection([Encoding.ASCII.GetBytes(request)], initialTlsSession: Session(applicationProtocol));

        await Server(stream).ServeAsync(connection, ContextFor(DnsOverHttps));

        var written = Encoding.ASCII.GetString(connection.WrittenBytes);
        StringAssert.StartsWith(written, "HTTP/1.1 200");
        StringAssert.Contains(written, "application/dns-message");
        Assert.AreEqual(0, stream.Served);
    }

    [TestMethod]
    public async Task ServeAsync_DnsOverHttpsWithH2_ReadsThePrefaceAndAnswersWithSettings()
    {
        await using var connection = new InMemoryConnection([Encoding.ASCII.GetBytes("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n")], initialTlsSession: Session("h2"));

        await Server(new RecordingConnectionServer()).ServeAsync(connection, ContextFor(DnsOverHttps));

        // The first frame the server writes is its SETTINGS: type 4 in the ninth-byte-less header's fourth byte.
        Assert.IsGreaterThanOrEqualTo(9, connection.WrittenBytes.Length);
        Assert.AreEqual(4, connection.WrittenBytes[3]);
    }

    [TestMethod]
    public async Task ServeAsync_DnsOverHttpsWithH2AndNotThePreface_WritesNothing()
    {
        await using var connection = new InMemoryConnection([Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n\r\n")], initialTlsSession: Session("h2"));

        await Server(new RecordingConnectionServer()).ServeAsync(connection, ContextFor(DnsOverHttps));

        Assert.IsEmpty(connection.WrittenBytes);
    }


    private static AuthenticationPolicy PolicyWithAlice()
    {
        var accounts = new AccountBook([new Account("alice", "secret")]);
        return new AuthenticationPolicy(
            new AuthenticationSettings(accounts, AllowAnonymous: false, AllowPlaintextAuthentication: true, AuthenticationMethods.DefaultAccepted),
            [new BasicAuthenticationMethod(accounts)],
            TimeProvider.System);
    }

    [TestMethod]
    [DataRow(null, "HTTP/1.1 401")]
    [DataRow("Authorization: Basic YWxpY2U6c2VjcmV0\r\n", "HTTP/1.1 200")]
    public async Task ServeAsync_DnsOverHttpsWithAnAccount_ChallengesWithoutBasicCredentialsAndAnswersWithThem(string? authorization, string status)
    {
        var request = $"GET /dns-query?dns={ExampleGetParameter} HTTP/1.1\r\nHost: dnsserver.example.net\r\n{authorization}Connection: close\r\n\r\n";
        await using var connection = new InMemoryConnection([Encoding.ASCII.GetBytes(request)], initialTlsSession: Session("http/1.1"));
        var server = new DnsConnectionRoutingProtocolServer(new RecordingConnectionServer(), new DnsZoneSet(), allowZoneTransfer: false, PolicyWithAlice());

        await server.ServeAsync(connection, ContextFor(DnsOverHttps));

        var written = Encoding.ASCII.GetString(connection.WrittenBytes);
        StringAssert.StartsWith(written, status);
        Assert.AreEqual(authorization is null, written.Contains("WWW-Authenticate: Basic realm=\"surl\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ServeAsync_DnsOverHttpsPostWithNoAccount_AnswersWithoutAskingForALogin()
    {
        byte[] query = [0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 3, 119, 119, 119, 7, 101, 120, 97, 109, 112, 108, 101, 3, 99, 111, 109, 0, 0, 1, 0, 1];
        var head = Encoding.ASCII.GetBytes("POST /dns-query HTTP/1.1\r\nHost: a\r\nContent-Type: application/dns-message\r\nContent-Length: 33\r\nConnection: close\r\n\r\n");
        await using var connection = new InMemoryConnection([(byte[])[.. head, .. query]], initialTlsSession: Session("http/1.1"));
        var noAccounts = new AuthenticationPolicy(
            new AuthenticationSettings(new AccountBook([]), AllowAnonymous: false, AllowPlaintextAuthentication: false, AuthenticationMethods.DefaultAccepted),
            [new BasicAuthenticationMethod(new AccountBook([]))],
            TimeProvider.System);
        var server = new DnsConnectionRoutingProtocolServer(new RecordingConnectionServer(), new DnsZoneSet(), allowZoneTransfer: false, noAccounts);

        await server.ServeAsync(connection, ContextFor(DnsOverHttps));

        var written = Encoding.ASCII.GetString(connection.WrittenBytes);
        StringAssert.StartsWith(written, "HTTP/1.1 200");
        Assert.DoesNotContain("WWW-Authenticate", written);
    }
    [TestMethod]
    public void ServeAsync_NullArguments_Throw()
    {
        var server = Server(new RecordingConnectionServer());

        Assert.ThrowsExactly<ArgumentNullException>(() => server.ServeAsync(null!, ContextFor(DnsOverHttps)));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.ServeAsync(new InMemoryConnection([]), null!));
    }
}
