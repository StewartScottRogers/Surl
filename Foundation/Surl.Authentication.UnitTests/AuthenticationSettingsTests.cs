namespace Surl.Authentication;

[TestClass]
public sealed class AuthenticationSettingsTests
{
    [TestMethod]
    public void HasAccountToLogInTo_NoAccountAndNoDirectory_IsFalse() =>
        Assert.IsFalse(Settings([], null).HasAccountToLogInTo("coap"));

    [TestMethod]
    public void HasAccountToLogInTo_AnAccountAndNoDirectory_IsTrue() =>
        Assert.IsTrue(Settings([new Account("alice", "pw")], null).HasAccountToLogInTo("coap"));

    [TestMethod]
    [DataRow(null, true, DisplayName = "a directory serving every scheme")]
    [DataRow("coaps", true, DisplayName = "a directory naming the TLS twin")]
    [DataRow("ftp", false, DisplayName = "a directory leaving the scheme out")]
    public void HasAccountToLogInTo_DirectoryAlone_FollowsItsProtocols(string? protocol, bool expected)
    {
        Assert.IsTrue(LdapDirectoryUrl.TryParse("ldap://dir.test/dc=example,dc=com", out var url, out _));
        var directory = new LdapAccountSource(
            new LdapAccountSourceSettings(url!) { Protocols = protocol is null ? null : [protocol] },
            new ScriptedLdapDirectory(),
            TimeProvider.System);

        Assert.AreEqual(expected, Settings([], directory).HasAccountToLogInTo("coap"));
    }

    [TestMethod]
    public void HasAccountToLogInTo_NullScheme_IsRefused() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Settings([], null).HasAccountToLogInTo(null!));

    [TestMethod]
    public void RequiresLoginWithoutAccounts_ByDefault_IsFalse() =>
        Assert.IsFalse(Settings([], null).RequiresLoginWithoutAccounts);

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ForScheme_KeepsRequiresLoginWithoutAccounts(bool requiresLogin)
    {
        var settings = Settings([], null) with { RequiresLoginWithoutAccounts = requiresLogin };

        var view = settings.ForScheme("http");

        Assert.AreEqual(requiresLogin, view.RequiresLoginWithoutAccounts);
    }

    private static AuthenticationSettings Settings(IReadOnlyList<Account> accounts, LdapAccountSource? directory) =>
        new(new AccountBook(accounts), AllowAnonymous: false, AllowPlaintextAuthentication: false, AuthenticationMethods.DefaultAccepted)
        {
            LdapAccountSource = directory,
        };
}
