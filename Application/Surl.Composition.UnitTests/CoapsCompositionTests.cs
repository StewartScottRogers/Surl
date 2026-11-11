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
    public void LookUpKey_Identity_IsTheAccountsPasswordOrNothing()
    {
        var lookup = DeferredCoapsProtocolServer.LookUpKey([new Account("alice", "pw"), new Account("bob", "other")]);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("other"), lookup(Encoding.UTF8.GetBytes("bob")));
        Assert.IsNull(lookup(Encoding.UTF8.GetBytes("carol")));
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

    [TestMethod]
    public void ReadPreSharedKeyAccounts_UserOptionAndFile_AreTheAccountsWithPasswords()
    {
        var commandLine = Parse("--user", "alice:pw", "--user-file", "users.txt", "coaps://127.0.0.1:0/");

        var accounts = AuthenticationComposition.ReadPreSharedKeyAccounts(commandLine, _ => Encoding.ASCII.GetBytes("bob:other\n"));

        CollectionAssert.AreEqual(new[] { "alice", "bob" }, accounts.Select(account => account.UserName).ToArray());
        Assert.AreEqual("other", accounts[1].Password);
    }

    [TestMethod]
    public void ReadPreSharedKeyAccounts_UnreadableFile_IsNone()
    {
        var commandLine = Parse("--user-file", "users.txt", "coaps://127.0.0.1:0/");

        Assert.IsEmpty(AuthenticationComposition.ReadPreSharedKeyAccounts(commandLine, _ => throw new IOException()));
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

    private static ComposedProtocolServices ComposeServices(bool allowAnonymous, IReadOnlyList<Account> accounts)
    {
        var unserved = SurlServerComposition.ComposeUnservedProtocolServices(
            SurlServerComposition.ComposeContentStore(new SurlCommandLine(), TimeProvider.System), TimeProvider.System);
        return new ComposedProtocolServices(
            unserved.ContentStore,
            new ServiceState(unserved.MessageBus, unserved.MailStore, unserved.LdapServer, unserved.ObjectMetadataIndex),
            new AuthenticationPolicy(
                new AuthenticationSettings(new AccountBook(accounts), allowAnonymous, AllowPlaintextAuthentication: false, AuthenticationMethods.DefaultAccepted),
                [],
                TimeProvider.System),
            TimeProvider.System,
            unserved.SshHostKeys,
            unserved.SshAlgorithms,
            isTlsUpgradeAvailable: false,
            echoesWebSocketMessages: false,
            advertisesHttp3: false)
        {
            PreSharedKeyAccounts = accounts,
        };
    }
}
