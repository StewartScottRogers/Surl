using System.Security.Cryptography;
using System.Text;
using Surl.Authentication;
using Surl.Cli;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class CoapsCompositionTests
{
    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, false, true)]
    [DataRow(false, true, false)]
    public void CoapAccessRulesFor_Policy_IsAnonymousSwitchAndWhetherAnAccountExists(bool allowAnonymous, bool hasAccount, bool expectedAnonymous)
    {
        var services = ComposeServices(allowAnonymous, hasAccount ? [new Account("alice", "pw")] : []);

        var rules = ExtendedProtocolServers.CoapAccessRulesFor(services);

        Assert.AreEqual(expectedAnonymous, rules.AllowsAnonymous);
        Assert.AreEqual(hasAccount, rules.HasAccounts);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "a directory serving every scheme")]
    [DataRow("coap", DisplayName = "a directory naming coap")]
    [DataRow("coaps", DisplayName = "a directory naming coaps")]
    public void CoapAccessRulesFor_DirectoryOnlyServingCoap_RefusesAnUnauthenticatedRead(string? protocol)
    {
        var services = ComposeServices(false, [], DirectoryServing(protocol));

        var rules = ExtendedProtocolServers.CoapAccessRulesFor(services);

        Assert.IsFalse(rules.MayRead(peerIsAuthenticated: false));
        Assert.IsTrue(rules.MayRead(peerIsAuthenticated: true));
    }

    [TestMethod]
    [DataRow(false, "smtp", DisplayName = "a directory serving neither coap nor coaps")]
    [DataRow(true, null, DisplayName = "--allow-anonymous beside a directory")]
    public void CoapAccessRulesFor_DirectoryNotMakingCoapPrivate_ServesAnUnauthenticatedRead(bool allowAnonymous, string? protocol)
    {
        var services = ComposeServices(allowAnonymous, [], DirectoryServing(protocol));

        var rules = ExtendedProtocolServers.CoapAccessRulesFor(services);

        Assert.IsTrue(rules.MayRead(peerIsAuthenticated: false));
    }

    [TestMethod]
    public void LookUpKey_Identity_IsTheLiveBooksPasswordOrNothing()
    {
        var book = new AccountBook([new Account("alice", "pw"), new Account("bob", "other"), new Account("mail", "x") { Schemes = new HashSet<string> { "smtp" } }]);
        var lookup = DeferredCoapsProtocolServer.LookUpKey(book);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("other"), lookup(Encoding.UTF8.GetBytes("bob")));
        Assert.IsNull(lookup(Encoding.UTF8.GetBytes("carol")));
        Assert.IsNull(lookup(Encoding.UTF8.GetBytes("mail")));
        book.Add(new Account("carol", "new"));
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("new"), lookup(Encoding.UTF8.GetBytes("carol")));
        book.Remove("bob");
        Assert.IsNull(lookup(Encoding.UTF8.GetBytes("bob")));
    }

    [TestMethod]
    public async Task ServeAsync_CertificateAndKeyAndAnchors_BuildsTheServerOnTheFirstFlow()
    {
        var services = ComposeServices(false, [new Account("alice", "pw"), new Account("mail", "pw") { Schemes = new HashSet<string> { "smtp" } }]);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        services.QuicCertificate.Certificate = new QuicServerCertificate([new byte[] { 1 }], key);
        using var anchor = ThrowawayServerCertificate.Create(TimeProvider.System, ["127.0.0.1"]);
        services.QuicCertificate.ClientTrustAnchors = [anchor];
        var server = new DeferredCoapsProtocolServer(services);

        await ServeCancelledAsync(server);
        await ServeCancelledAsync(server);

        CollectionAssert.AreEqual(new[] { "coaps" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_AccountsAloneWithNoCertificate_BuildsAPreSharedKeyServer()
    {
        var server = new DeferredCoapsProtocolServer(ComposeServices(false, [new Account("alice", "pw")]));

        await ServeCancelledAsync(server);
    }

    [TestMethod]
    public async Task ServeAsync_NeitherCertificateNorAccount_RefusesToBuild()
    {
        var server = new DeferredCoapsProtocolServer(ComposeServices(false, []));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await using var flow = new InMemoryDatagramFlow(new byte[1], []);

        await Assert.ThrowsAsync<ArgumentException>(() => server.ServeAsync(flow, ContextFor(cancelled.Token)));
    }

    [TestMethod]
    public void FindRefusal_NoCertificateOrNoCoapsListenUrl_IsNull()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP521);
        var certificate = new QuicServerCertificate([new byte[] { 1 }], key);

        Assert.IsNull(CoapsDtlsCertificate.FindRefusal(CommandLineFor("coaps"), null));
        Assert.IsNull(CoapsDtlsCertificate.FindRefusal(CommandLineFor("coap"), certificate));
    }

    [TestMethod]
    public void FindRefusal_KeyDtlsCanSignWith_IsNull()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        Assert.IsNull(CoapsDtlsCertificate.FindRefusal(CommandLineFor("coaps"), new QuicServerCertificate([new byte[] { 1 }], key)));
    }

    [TestMethod]
    public void FindRefusal_KeyDtlsCannotSignWith_Is58()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP521);

        var refusal = CoapsDtlsCertificate.FindRefusal(CommandLineFor("coaps"), new QuicServerCertificate([new byte[] { 1 }], key));

        StringAssert.StartsWith(refusal, "(58) Could not load the server certificate: ");
    }

    [TestMethod]
    public void FindListenUrlWithoutCertificate_CoapsWithNoAccountOrCertificate_IsTheListenUrl()
    {
        Assert.AreEqual(
            new ListenUrl("coaps", "127.0.0.1", 0), ServerTlsComposition.FindListenUrlWithoutCertificate(Parse("coaps://127.0.0.1:0/")));
    }

    [TestMethod]
    public void FindListenUrlWithoutCertificate_CoapsWithAnAccount_IsNull()
    {
        Assert.IsNull(ServerTlsComposition.FindListenUrlWithoutCertificate(Parse("--user", "alice:pw", "coaps://127.0.0.1:0/")));
        Assert.IsNull(ServerTlsComposition.FindListenUrlWithoutCertificate(Parse("--user-file", "users.txt", "coaps://127.0.0.1:0/")));
    }

    [TestMethod]
    public void Compose_CoapsWithSelfSigned_HasTheCertificateAndNoAnchors()
    {
        using var composition = ServerTlsComposition.Compose(Parse("--self-signed", "coaps://127.0.0.1:0/"), TimeProvider.System);

        Assert.IsNotNull(composition.QuicCertificate);
        Assert.IsEmpty(composition.ClientTrustAnchors);
    }

    [TestMethod]
    public void Compose_CoapsWithAnAccountAndNoCertificate_ServesNoCertificate()
    {
        using var composition = ServerTlsComposition.Compose(Parse("--user", "alice:pw", "coaps://127.0.0.1:0/"), TimeProvider.System);

        Assert.IsNull(composition.QuicCertificate);
        Assert.IsNull(composition.Settings);
    }

    private static SurlCommandLine Parse(params string[] args) =>
        CommandLineParser.Parse(args, ["coap", "coaps"]).CommandLine ?? throw new AssertFailedException("The command line was refused.");

    private static SurlCommandLine CommandLineFor(string scheme) =>
        new() { ListenUrls = [new ListenUrl(scheme, "127.0.0.1", 0)] };

    private static ExchangeContext ContextFor(CancellationToken token) =>
        new(
            1,
            new ListenUrl("coaps", "127.0.0.1", 1),
            new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 1),
            new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 2),
            new RecordingExchangeLog(),
            TimeProvider.System,
            token);

    private static async Task ServeCancelledAsync(DeferredCoapsProtocolServer server)
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await using var flow = new InMemoryDatagramFlow(new byte[1], []);
        try
        {
            await server.ServeAsync(flow, ContextFor(cancelled.Token));
        }
        catch (OperationCanceledException)
        {
            // The cancelled exchange stops the flow once the server is built.
        }
    }

    private static LdapAccountSource DirectoryServing(string? protocol)
    {
        Assert.IsTrue(LdapDirectoryUrl.TryParse("ldap://dir.test/dc=example,dc=com", out var url, out _));

        return new LdapAccountSource(
            new LdapAccountSourceSettings(url!) { Protocols = protocol is null ? null : [protocol] },
            new UnreachedDirectory(),
            TimeProvider.System);
    }

    private static ComposedProtocolServices ComposeServices(
        bool allowAnonymous, IReadOnlyList<Account> accounts, LdapAccountSource? directory = null)
    {
        var unserved = SurlServerComposition.ComposeUnservedProtocolServices(
            SurlServerComposition.ComposeContentStore(new SurlCommandLine(), TimeProvider.System), TimeProvider.System);
        return new ComposedProtocolServices(
            unserved.ContentStore,
            new ServiceState(unserved.MessageBus, unserved.MailStore, unserved.LdapServer, unserved.ObjectMetadataIndex),
            new AuthenticationPolicy(
                new AuthenticationSettings(new AccountBook(accounts), allowAnonymous, AllowPlaintextAuthentication: false, AuthenticationMethods.DefaultAccepted)
                {
                    LdapAccountSource = directory,
                },
                [],
                TimeProvider.System),
            TimeProvider.System,
            unserved.SshHostKeys,
            unserved.SshAlgorithms,
            isTlsUpgradeAvailable: false,
            echoesWebSocketMessages: false,
            advertisesHttp3: false);
    }

    // CoAP carries no password, so its access rules never reach the directory.
    private sealed class UnreachedDirectory : ILdapDirectoryConnector
    {
        public ValueTask<ILdapDirectoryConnection> ConnectAsync(LdapDirectoryEndpoint endpoint, CancellationToken cancellationToken) =>
            throw new IOException("no directory in this test");
    }
}
