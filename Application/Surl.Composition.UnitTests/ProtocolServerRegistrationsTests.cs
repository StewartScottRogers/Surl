using Surl.Cli;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Dict;
using Surl.Protocol.Ftp;
using Surl.Protocol.Gopher;
using Surl.Protocol.Http;
using Surl.Protocol.Imap;
using Surl.Protocol.Ldap;
using Surl.Protocol.Mqtt;
using Surl.Protocol.Pop3;
using Surl.Protocol.Rtsp;
using Surl.Protocol.Smb;
using Surl.Protocol.Smtp;
using Surl.Protocol.Ssh;
using Surl.Protocol.Telnet;
using Surl.Protocol.Tftp;
using Surl.Protocol.Ws;

namespace Surl.Composition;

[TestClass]
public sealed class ProtocolServerRegistrationsTests
{
    [TestMethod]
    public void Original_Composed_AreTheServerTypesAndSchemesInTheOrderSurlHasAlwaysRegisteredThem()
    {
        var servers = SurlServerComposition.ComposeProtocolServers(ProtocolServerRegistrations.Original, UnservedServices());

        CollectionAssert.AreEqual(
            new[]
            {
                typeof(HttpProtocolServer), typeof(ImplicitTlsSchemeServer), typeof(DictProtocolServer),
                typeof(FtpProtocolServer), typeof(GopherProtocolServer), typeof(ImapProtocolServer),
                typeof(ImplicitTlsSchemeServer), typeof(LdapProtocolServer), typeof(ImplicitTlsSchemeServer),
                typeof(MqttProtocolServer), typeof(Pop3ProtocolServer), typeof(ImplicitTlsSchemeServer),
                typeof(RtspProtocolServer), typeof(SmbProtocolServer), typeof(SmtpProtocolServer),
                typeof(ImplicitTlsSchemeServer), typeof(SingleSchemeServer), typeof(SingleSchemeServer), typeof(TelnetProtocolServer),
                typeof(TftpProtocolServer), typeof(WsProtocolServer), typeof(ImplicitTlsSchemeServer),
            },
            servers.Select(server => server.GetType()).ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "http", "https", "dict", "ftp ftps", "gopher gophers", "imap", "imaps", "ldap", "ldaps", "mqtt mqtts",
                "pop3", "pop3s", "rtsp", "smb smbs", "smtp", "smtps", "scp", "sftp", "telnet", "tftp", "ws", "wss",
            },
            servers.Select(server => string.Join(' ', server.Schemes)).ToArray());
    }

    [TestMethod]
    public void Original_Composed_EachImplicitTlsSchemeIsAnsweredByTheServerOfItsPlainScheme()
    {
        var servers = SurlServerComposition.ComposeProtocolServers(ProtocolServerRegistrations.Original, UnservedServices());

        var pairs = servers.OfType<ImplicitTlsSchemeServer>()
            .Select(pair => (pair.Schemes[0], servers.Single(server => ReferenceEquals(server, pair.Server)).Schemes[0]))
            .ToArray();

        CollectionAssert.AreEqual(
            new[] { ("https", "http"), ("imaps", "imap"), ("ldaps", "ldap"), ("pop3s", "pop3"), ("smtps", "smtp"), ("wss", "ws") },
            pairs);
    }

    [TestMethod]
    public void Original_EveryEntry_NamesExactlyTheSchemesItsServerAnswers()
    {
        var servers = SurlServerComposition.ComposeProtocolServers(ProtocolServerRegistrations.Original, UnservedServices());

        CollectionAssert.AreEqual(
            ProtocolServerRegistrations.Original.Select(registration => string.Join(' ', registration.Schemes)).ToArray(),
            servers.Select(server => string.Join(' ', server.Schemes)).ToArray());
    }

    [TestMethod]
    public void ComposeProtocolServers_TheOriginalsAndOneTestOnlyEntry_ComposesThatServerBesideThem()
    {
        var testServer = new SchemeOnlyServer("x-test");
        IReadOnlyList<ProtocolServerRegistration> registrations =
            [.. ProtocolServerRegistrations.Original, new(["x-test"], _ => testServer)];

        var servers = SurlServerComposition.ComposeProtocolServers(registrations, UnservedServices());

        Assert.HasCount(ProtocolServerRegistrations.Original.Count + 1, servers);
        Assert.AreSame(testServer, servers[^1]);
        CollectionAssert.AreEqual(
            SurlServerComposition.ComposeRegisteredSchemes(TimeProvider.System).Append("x-test").ToArray(),
            servers.SelectMany(server => server.Schemes).ToArray());
    }

    [TestMethod]
    public void ComposeProtocolServers_NullRegistrations_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => SurlServerComposition.ComposeProtocolServers(null!, UnservedServices()));
    }

    [TestMethod]
    public void ComposeProtocolServers_NullServices_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => SurlServerComposition.ComposeProtocolServers(ProtocolServerRegistrations.Original, null!));
    }

    [TestMethod]
    public void ComposedServerFor_ASchemeNoEarlierEntryRegistered_ThrowsInvalidOperationException()
    {
        IReadOnlyList<ProtocolServerRegistration> registrations = [ProtocolServerRegistration.ImplicitTls("https", "http")];

        var failure = Assert.ThrowsExactly<InvalidOperationException>(
            () => SurlServerComposition.ComposeProtocolServers(registrations, UnservedServices()));

        Assert.AreEqual("No protocol server registered before this entry answers http.", failure.Message);
    }

    internal static ComposedProtocolServices UnservedServices() => SurlServerComposition.ComposeUnservedProtocolServices(
        SurlServerComposition.ComposeContentStore(new SurlCommandLine(), TimeProvider.System), TimeProvider.System);

    private sealed class SchemeOnlyServer(string scheme) : IConnectionProtocolServer
    {
        public IReadOnlyList<string> Schemes { get; } = [scheme];

        public Task ServeAsync(IConnection connection, ExchangeContext context) => Task.CompletedTask;
    }
}
