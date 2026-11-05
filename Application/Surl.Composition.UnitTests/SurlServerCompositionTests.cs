using System.Security.Cryptography;
using Surl.Cli;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Ssh;

namespace Surl.Composition;

[TestClass]
public sealed class SurlServerCompositionTests
{
    [TestMethod]
    public void ComposeRegisteredSchemes_ThisBuild_AreEveryOriginalServersSchemesInRegistrationOrder()
    {
        var schemes = SurlServerComposition.ComposeRegisteredSchemes(TimeProvider.System);

        CollectionAssert.AreEqual(
            new[]
            {
                "http", "https", "dict", "ftp", "ftps", "gopher", "gophers", "imap", "imaps", "ldap", "ldaps",
                "mqtt", "mqtts", "pop3", "pop3s", "rtsp", "smb", "smbs", "smtp", "smtps", "scp", "sftp",
                "telnet", "tftp", "ws", "wss",
            },
            schemes.ToArray());
    }

    [TestMethod]
    public void ComposeExtendedSchemes_TurnedOnWithASeam_AreTheSeamsServersSchemes()
    {
        var schemes = SurlServerComposition.ComposeExtendedSchemes(
            areExtensionsTurnedOn: true, _ => [new SchemeOnlyServer("s3")], TimeProvider.System);

        CollectionAssert.AreEqual(new[] { "s3" }, schemes.ToArray());
    }

    [TestMethod]
    public void ComposeExtendedSchemes_TurnedOnWithoutASeam_AreThisBuildsExtendedServersSchemes()
    {
        var schemes = SurlServerComposition.ComposeExtendedSchemes(areExtensionsTurnedOn: true, null, TimeProvider.System);

        CollectionAssert.AreEqual(
            ExtendedProtocolServers.Compose(Services()).SelectMany(server => server.Schemes).ToArray(),
            schemes.ToArray());
    }

    [TestMethod]
    public void ComposeExtendedProtocolServers_TurnedOff_NeverCallsTheSeam()
    {
        var servers = SurlServerComposition.ComposeExtendedProtocolServers(
            areExtensionsTurnedOn: false, _ => throw new AssertFailedException("The seam must not be called."), Services());

        Assert.IsEmpty(servers);
    }

    [TestMethod]
    public void ComposePolicyWithoutAccounts_AnyClock_AllowsNeitherAnonymousNorPlaintext()
    {
        var policy = SurlServerComposition.ComposePolicyWithoutAccounts(TimeProvider.System);

        Assert.IsFalse(policy.Settings.AllowAnonymous);
        Assert.IsFalse(policy.Settings.AllowPlaintextAuthentication);
    }

    [TestMethod]
    public void ComposeProtocolServers_NullServiceState_ThrowsArgumentNullException()
    {
        var policy = SurlServerComposition.ComposePolicyWithoutAccounts(TimeProvider.System);

        Assert.ThrowsExactly<ArgumentNullException>(() => SurlServerComposition.ComposeProtocolServers(
            SurlServerComposition.ComposeContentStore(new SurlCommandLine(), TimeProvider.System),
            null!,
            policy,
            new SshHostKeySet(),
            SshAlgorithmOffer.Default([], AesGcm.IsSupported),
            isTlsUpgradeAvailable: false,
            echoesWebSocketMessages: false));
    }

    private static ExtendedProtocolServerServices Services() => new(
        SurlServerComposition.ComposeContentStore(new SurlCommandLine(), TimeProvider.System),
        SurlServerComposition.ComposePolicyWithoutAccounts(TimeProvider.System),
        IsTlsUpgradeAvailable: false);

    private sealed class SchemeOnlyServer(string scheme) : IConnectionProtocolServer
    {
        public IReadOnlyList<string> Schemes { get; } = [scheme];

        public Task ServeAsync(IConnection connection, ExchangeContext context) => Task.CompletedTask;
    }
}
