namespace Surl.Authentication;

[TestClass]
public sealed class AuthenticationSettingsTests
{
    [TestMethod]
    public void IsLoginRequiredToRead_NoAccountAndNoDirectory_IsFalse() =>
        Assert.IsFalse(Settings([], null).IsLoginRequiredToRead("coap"));

    [TestMethod]
    public void IsLoginRequiredToRead_AnAccountAndNoDirectory_IsTrue() =>
        Assert.IsTrue(Settings([new Account("alice", "pw")], null).IsLoginRequiredToRead("coap"));

    [TestMethod]
    public void IsLoginRequiredToRead_AfterTheLastAccountIsRemoved_StaysTrueWithNoAccountHeld()
    {
        var settings = Settings([new Account("alice", "pw")], null);

        settings.Accounts.Remove("alice");

        Assert.IsFalse(settings.Accounts.HasAccounts);
        Assert.IsTrue(settings.IsLoginRequiredToRead("coap"));
    }

    [TestMethod]
    public void IsLoginRequiredToRead_AfterAPasswordChangeAndARemoval_StaysTrue()
    {
        var settings = Settings([], null);
        settings.Accounts.Add(new Account("alice", "pw"));

        settings.Accounts.SetPassword("alice", "other");
        settings.Accounts.Remove("alice");

        Assert.IsTrue(settings.IsLoginRequiredToRead("coap"));
    }

    [TestMethod]
    [DataRow(null, null, "ftp", true, DisplayName = "two unrestricted accounts")]
    [DataRow(null, "smtp", "ftp", true, DisplayName = "unrestricted, then restricted")]
    [DataRow("smtp", null, "ftp", true, DisplayName = "restricted, then unrestricted")]
    [DataRow("smtp", "smtp", "smtps", true, DisplayName = "the same restriction twice")]
    [DataRow("smtp", "smtp", "ftp", false, DisplayName = "the same restriction twice, another scheme")]
    [DataRow("smtp", "imap", "smtp", true, DisplayName = "two restrictions, the first's scheme")]
    [DataRow("smtp", "imap", "imap", true, DisplayName = "two restrictions, the second's scheme")]
    [DataRow("smtp", "imap", "ftp", false, DisplayName = "two restrictions, a scheme of neither")]
    public void IsLoginRequiredToRead_AfterBothAccountsAreRemoved_FollowsTheSchemesEachMightLogInOn(
        string? first, string? second, string scheme, bool expected)
    {
        var settings = Settings([Restricted("alice", first), Restricted("bob", second)], null);

        settings.Accounts.Remove("alice");
        settings.Accounts.Remove("bob");

        Assert.AreEqual(expected, settings.ForScheme(scheme).IsLoginRequiredToRead(scheme));
    }

    [TestMethod]
    public void IsLoginRequiredToRead_AfterTheCallerChangesARemovedAccountsSchemes_KeepsTheSchemesItHad()
    {
        var schemes = new HashSet<string> { "http" };
        var settings = Settings([new Account("alice", "pw") { Schemes = schemes }], null);
        settings.Accounts.Remove("alice");

        schemes.Clear();

        Assert.IsTrue(settings.ForScheme("http").IsLoginRequiredToRead("http"));
    }

    [TestMethod]
    [DataRow("imap", false, DisplayName = "the outer view's account alone")]
    [DataRow("smtp", true, DisplayName = "an account admitted to both views")]
    public void IsLoginRequiredToRead_OnAViewOfAViewAfterTheRemoval_NeedsBothSchemesAdmitted(string heldScheme, bool expected)
    {
        var accounts = new AccountBook([new Account("alice", "pw") { Schemes = new HashSet<string> { heldScheme, "http" } }]);
        var settings = new AuthenticationSettings(accounts.ForScheme("http").ForScheme("smtp"), false, false, AuthenticationMethods.DefaultAccepted);

        accounts.Remove("alice");

        Assert.AreEqual(expected, settings.IsLoginRequiredToRead("smtp"));
    }

    [TestMethod]
    public void IsLoginRequiredToRead_AfterReset_ForgetsOnlyTheAccountsAddedSinceTheStart()
    {
        var settings = Settings([Restricted("alice", "smtp")], null);
        settings.Accounts.Add(Restricted("bob", "http"));
        settings.Accounts.Remove("bob");

        settings.Accounts.Reset();

        Assert.IsFalse(settings.ForScheme("http").IsLoginRequiredToRead("http"));
        Assert.IsTrue(settings.ForScheme("smtp").IsLoginRequiredToRead("smtp"));
    }

    [TestMethod]
    public void IsLoginRequiredToRead_AfterRestoringASnapshotTakenAfterTheRemoval_IsTrueWithNoAccountHeld()
    {
        var settings = Settings([], null);
        settings.Accounts.Add(new Account("alice", "pw"));
        settings.Accounts.Remove("alice");
        var snapshot = settings.Accounts.TakeSnapshot();
        settings.Accounts.Reset();

        settings.Accounts.Restore(snapshot);

        Assert.IsEmpty(snapshot.UserNames);
        Assert.IsTrue(settings.IsLoginRequiredToRead("coap"));
    }

    private static Account Restricted(string userName, string? scheme) =>
        new(userName, "pw") { Schemes = scheme is null ? null : new HashSet<string> { scheme } };

    [TestMethod]
    [DataRow(null, true, DisplayName = "a directory serving every scheme")]
    [DataRow("coaps", true, DisplayName = "a directory naming the TLS twin")]
    [DataRow("ftp", false, DisplayName = "a directory leaving the scheme out")]
    public void IsLoginRequiredToRead_DirectoryAlone_FollowsItsProtocols(string? protocol, bool expected)
    {
        Assert.IsTrue(LdapDirectoryUrl.TryParse("ldap://dir.test/dc=example,dc=com", out var url, out _));
        var directory = new LdapAccountSource(
            new LdapAccountSourceSettings(url!) { Protocols = protocol is null ? null : [protocol] },
            new ScriptedLdapDirectory(),
            TimeProvider.System);

        Assert.AreEqual(expected, Settings([], directory).IsLoginRequiredToRead("coap"));
    }

    [TestMethod]
    public void IsLoginRequiredToRead_NullScheme_IsRefused() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Settings([], null).IsLoginRequiredToRead(null!));

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
