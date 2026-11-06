using System.Security.Cryptography;
using Surl.Authentication;
using Surl.Cli;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Imap;
using Surl.Protocol.Smtp;
using Surl.Protocol.Ssh;

namespace Surl.Composition;

[TestClass]
public sealed class SchemeAuthenticationPoliciesTests
{
    [TestMethod]
    public void For_NoAccountRestricted_IsAViewAdmittingEveryAccountTheOnePolicyAdmits()
    {
        var policy = Policy(new Account("ann", "secret"));

        var ftp = new SchemeAuthenticationPolicies(policy, TimeProvider.System).For("ftp");

        Assert.IsTrue(ftp.Settings.Accounts.CheckPassword("ann", "secret"u8));
        CollectionAssert.AreEqual(policy.Settings.Accounts.UserNames.ToArray(), ftp.Settings.Accounts.UserNames.ToArray());
    }

    [TestMethod]
    public void For_AnAccountRestrictedElsewhere_IsOneCachedViewPerSchemeWithoutIt()
    {
        var policy = Policy(new Account("ann", "secret") { Schemes = Account.MailSchemes }, new Account("bob", "pw"));
        var policies = new SchemeAuthenticationPolicies(policy, TimeProvider.System);

        var ftp = policies.For("ftp");

        Assert.AreNotSame(policy, ftp);
        Assert.AreSame(ftp, policies.For("ftp"));
        Assert.AreSame(ftp, policies.For("FTP"));
        Assert.AreNotSame(ftp, policies.For("imap"));
        Assert.IsFalse(ftp.Settings.Accounts.CheckPassword("ann", "secret"u8));
        Assert.IsTrue(ftp.Settings.Accounts.CheckPassword("bob", "pw"u8));
        Assert.IsTrue(policies.For("imap").Settings.Accounts.CheckPassword("ann", "secret"u8));
    }

    [TestMethod]
    public void For_ARestrictedAccountAddedAfterTheViewWasComposed_IsRefusedOnOtherSchemes()
    {
        var policy = Policy(new Account("bob", "pw"));
        var policies = new SchemeAuthenticationPolicies(policy, TimeProvider.System);
        var ftp = policies.For("ftp");

        policy.Settings.Accounts.Add(new Account("ann", "secret") { Schemes = Account.MailSchemes });

        Assert.IsFalse(ftp.Settings.Accounts.CheckPassword("ann", "secret"u8));
        Assert.IsTrue(policies.For("imap").Settings.Accounts.CheckPassword("ann", "secret"u8));
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SchemeAuthenticationPolicies(null!, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SchemeAuthenticationPolicies(Policy(), null!));
    }

    [TestMethod]
    public void For_NullScheme_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new SchemeAuthenticationPolicies(Policy(), TimeProvider.System).For(null!));

    [TestMethod]
    public void AuthenticationPolicyFor_WithAndWithoutASchemeView_IsTheViewOrTheOnePolicy()
    {
        var unserved = ProtocolServerRegistrationsTests.UnservedServices();
        var view = Policy();
        var services = new ExtendedProtocolServerServices(unserved);
        var withView = new ExtendedProtocolServerServices(
            new ComposedProtocolServices(
                unserved.ContentStore,
                new ServiceState(unserved.MessageBus, unserved.MailStore, unserved.LdapServer, unserved.ObjectMetadataIndex),
                unserved.AuthenticationPolicy,
                TimeProvider.System,
                unserved.SshHostKeys,
                unserved.SshAlgorithms,
                isTlsUpgradeAvailable: false,
                echoesWebSocketMessages: false,
                advertisesHttp3: false)
            {
                AuthenticationPolicyForScheme = _ => view,
            });

        Assert.IsNull(services.AuthenticationPolicyForScheme);
        Assert.AreSame(unserved.AuthenticationPolicy, services.AuthenticationPolicyFor("s3"));
        Assert.IsNotNull(withView.AuthenticationPolicyForScheme);
        Assert.AreSame(view, withView.AuthenticationPolicyFor("s3"));
    }

    [TestMethod]
    public void ComposeProtocolServers_AnAccountRestrictedToImap_IsAcceptedOnImapAndRefusedOnSmtpAndHttp()
    {
        var policy = Policy(new Account("ann", "secret") { Schemes = new HashSet<string>(["imap"], StringComparer.OrdinalIgnoreCase) });
        var policies = new SchemeAuthenticationPolicies(policy, TimeProvider.System);
        var unserved = ProtocolServerRegistrationsTests.UnservedServices();

        var servers = SurlServerComposition.ComposeProtocolServers(
            unserved.ContentStore,
            new ServiceState(unserved.MessageBus, unserved.MailStore, unserved.LdapServer, unserved.ObjectMetadataIndex),
            policy,
            new SshHostKeySet(),
            SshAlgorithmOffer.Default([], AesGcm.IsSupported),
            isTlsUpgradeAvailable: false,
            echoesWebSocketMessages: false,
            authenticationPolicyForScheme: policies.For);

        Assert.IsTrue(policies.For("imap").Settings.Accounts.CheckPassword("ann", "secret"u8));
        Assert.IsTrue(policies.For("imaps").Settings.Accounts.CheckPassword("ann", "secret"u8));
        Assert.IsFalse(policies.For("smtp").Settings.Accounts.CheckPassword("ann", "secret"u8));
        Assert.IsFalse(policies.For("http").Settings.Accounts.CheckPassword("ann", "secret"u8));
        Assert.AreSame(policies.For("imap"), ComposedServerFields.Single<IAuthenticationPolicy>(servers.OfType<ImapProtocolServer>().Single()));
        Assert.AreSame(policies.For("smtp"), ComposedServerFields.Single<IAuthenticationPolicy>(servers.OfType<SmtpProtocolServer>().Single()));
    }

    [TestMethod]
    public void ComposeProtocolServers_ScpAndSftp_AreEachClaimedByExactlyOneServerOverItsOwnSchemesPolicy()
    {
        var policies = new SchemeAuthenticationPolicies(Policy(new Account("ann", "secret")), TimeProvider.System);
        var unserved = ProtocolServerRegistrationsTests.UnservedServices();

        var servers = SurlServerComposition.ComposeProtocolServers(
            unserved.ContentStore,
            new ServiceState(unserved.MessageBus, unserved.MailStore, unserved.LdapServer, unserved.ObjectMetadataIndex),
            unserved.AuthenticationPolicy,
            new SshHostKeySet(),
            SshAlgorithmOffer.Default([], AesGcm.IsSupported),
            isTlsUpgradeAvailable: false,
            echoesWebSocketMessages: false,
            authenticationPolicyForScheme: policies.For);

        foreach (var scheme in new[] { "scp", "sftp" })
        {
            var claiming = servers.Where(server => server.Schemes.Contains(scheme)).ToArray();
            Assert.HasCount(1, claiming);
            var ssh = ComposedServerFields.Single<IConnectionProtocolServer>(claiming[0]);
            Assert.IsInstanceOfType<SshProtocolServer>(ssh);
            Assert.AreSame(policies.For(scheme), ComposedServerFields.Single<ISshAuthenticationPolicy>(ssh));
        }
    }

    private static AuthenticationPolicy Policy(params Account[] accounts) =>
        AuthenticationComposition.ComposePolicy(
            new AuthenticationSettings(new AccountBook(accounts), false, false, AuthenticationMethods.DefaultAccepted), TimeProvider.System, null, null);
}
