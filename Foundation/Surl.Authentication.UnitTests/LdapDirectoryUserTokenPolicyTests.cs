using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="AuthenticationPolicy.HonoursTokenIssuedTo"/> for a directory user, a name only the LDAP
/// account source knows (ADR-0167, Amendment 3): honoured for one hour after a password login the
/// directory accepted through the same policy, and only while the account book has never held the name.
/// </summary>
[TestClass]
public sealed class LdapDirectoryUserTokenPolicyTests
{
    private const string Dn = "uid=carol,ou=people,dc=example,dc=com";

    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);

    private static readonly TimeSpan OneTick = TimeSpan.FromTicks(1);

    private readonly ManualTimeProvider clock = new();
    private readonly ScriptedLdapDirectory directory = new();

    private IAuthenticationPolicy Policy(
        string viewScheme = "oci",
        AccountBook? accounts = null,
        ISurlEventPublisher? events = null,
        IFaultPlan? faults = null,
        string[]? protocols = null)
    {
        Assert.IsTrue(LdapDirectoryUrl.TryParse("ldaps://dir.test/ou=people,dc=example,dc=com", out var url, out _));
        var source = new LdapAccountSource(
            new LdapAccountSourceSettings(url!) { Protocols = protocols }, directory, clock);
        var settings = new AuthenticationSettings(
            accounts ?? new AccountBook([new Account("alice", "secret")]), false, false, AuthenticationMethods.DefaultAccepted)
        {
            LdapAccountSource = source,
        }.ForScheme(viewScheme);

        return new AuthenticationPolicy(settings, [], clock, events, faults);
    }

    // Search (id 1) finds the user, then the user's bind (id 2) answers with the code.
    private void DirectoryAnswersTo(int bindCode)
    {
        directory.Answers.Clear();
        directory.Answers.Add(LdapTestMessages.SearchEntry(1, Dn));
        directory.Answers.Add(LdapTestMessages.SearchDone(1, 0));
        directory.Answers.Add(LdapTestMessages.BindResponse(2, bindCode));
    }

    private async Task<PasswordLoginVerdict> LogInAsync(IAuthenticationPolicy policy, string user = "carol", string scheme = "oci")
    {
        var check = policy.CheckPasswordLoginAsync(
            new PasswordLogin(scheme, user, Encoding.UTF8.GetBytes("dirpw"), PolicyFixture.Tls), CancellationToken.None).AsTask();
        if (!check.IsCompleted)
        {
            clock.Advance(AuthenticationPolicy.RefusalDelay);
        }

        return await check;
    }

    private async Task AcceptedByTheDirectoryAsync(IAuthenticationPolicy policy, string user = "carol", string scheme = "oci")
    {
        DirectoryAnswersTo(0);

        Assert.AreEqual(PasswordLoginVerdict.Accepted, await LogInAsync(policy, user, scheme));
    }

    [TestMethod]
    [DataRow("oci", "oci", DisplayName = "oci")]
    [DataRow("mcp", "mcp", DisplayName = "mcp")]
    [DataRow("mcp", "mcps", DisplayName = "the TLS twin mcps")]
    public async Task HonoursTokenIssuedTo_ADirectoryUserTheDirectoryAccepted_IsTrue(string viewScheme, string loginScheme)
    {
        var policy = Policy(viewScheme, protocols: [viewScheme]);
        await AcceptedByTheDirectoryAsync(policy, scheme: loginScheme);

        Assert.IsTrue(policy.HonoursTokenIssuedTo("carol"));
        Assert.IsFalse(policy.NamesAccount("carol"));
    }

    [TestMethod]
    public async Task HonoursTokenIssuedTo_AfterTheCheck_ContactsNoDirectoryAndPublishesNothing()
    {
        var events = new RecordingSurlEventPublisher();
        var policy = Policy(events: events);
        await AcceptedByTheDirectoryAsync(policy);
        var published = events.Events.Count;

        Assert.IsTrue(policy.HonoursTokenIssuedTo("carol"));
        Assert.AreEqual(1, directory.ConnectCount);
        Assert.HasCount(published, events.Events);
    }

    [TestMethod]
    public void HonoursTokenIssuedTo_AnAccountTheBookNames_IsTrueWithoutAnyLogin()
    {
        Assert.IsTrue(Policy().HonoursTokenIssuedTo("alice"));
    }

    [TestMethod]
    public void HonoursTokenIssuedTo_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Policy().HonoursTokenIssuedTo(null!));
    }

    [TestMethod]
    [DataRow("", DisplayName = "the empty name")]
    [DataRow("carol", DisplayName = "a name that never logged in")]
    [DataRow("Carol", DisplayName = "an accepted name in another case")]
    public async Task HonoursTokenIssuedTo_ANameNotAccepted_IsFalse(string name)
    {
        var policy = Policy();
        if (name == "Carol")
        {
            await AcceptedByTheDirectoryAsync(policy);
        }

        Assert.IsFalse(policy.HonoursTokenIssuedTo(name));
    }

    [TestMethod]
    [DataRow(49, DisplayName = "a wrong password")]
    [DataRow(53, DisplayName = "a disabled user's refused bind")]
    public async Task HonoursTokenIssuedTo_ANameWhoseBindWasRefused_IsFalse(int bindCode)
    {
        var policy = Policy();
        DirectoryAnswersTo(bindCode);

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, await LogInAsync(policy));
        Assert.IsFalse(policy.HonoursTokenIssuedTo("carol"));
    }

    [TestMethod]
    public async Task HonoursTokenIssuedTo_ANameRefusedWhileTheDirectoryIsDown_IsFalse()
    {
        var policy = Policy();
        directory.ConnectFailure = new IOException("scripted");

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, await LogInAsync(policy));
        Assert.IsFalse(policy.HonoursTokenIssuedTo("carol"));
    }

    [TestMethod]
    public async Task HonoursTokenIssuedTo_ALoginAFaultRefused_IsFalse()
    {
        var policy = Policy(faults: new SpendingFaultPlan(new FaultRule("f1", FaultKind.RefuseLogin) { Account = "carol" }));
        DirectoryAnswersTo(0);

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, await LogInAsync(policy));
        Assert.IsFalse(policy.HonoursTokenIssuedTo("carol"));
    }

    [TestMethod]
    public async Task HonoursTokenIssuedTo_OnASchemeTheDirectoryDoesNotServe_IsFalse()
    {
        var policy = Policy(protocols: ["imap"]);
        DirectoryAnswersTo(0);

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, await LogInAsync(policy));
        Assert.AreEqual(0, directory.ConnectCount);
        Assert.IsFalse(policy.HonoursTokenIssuedTo("carol"));
    }

    [TestMethod]
    public async Task HonoursTokenIssuedTo_ANameThatLoggedInOnAnotherPolicyOnly_IsFalse()
    {
        var accounts = new AccountBook([]);
        var other = Policy(accounts: accounts);
        var policy = Policy(accounts: accounts);
        await AcceptedByTheDirectoryAsync(other);

        Assert.IsFalse(policy.HonoursTokenIssuedTo("carol"));
    }

    [TestMethod]
    public async Task HonoursTokenIssuedTo_ANameTheBookHoldsForOtherSchemesOnly_IsFalse()
    {
        var accounts = new AccountBook([new Account("carol", "secret") { Schemes = new HashSet<string> { "imap" } }]);
        var policy = Policy(accounts: accounts);
        DirectoryAnswersTo(0);

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, await LogInAsync(policy));
        Assert.AreEqual(0, directory.ConnectCount);
        Assert.IsFalse(policy.HonoursTokenIssuedTo("carol"));
    }

    [TestMethod]
    public async Task HonoursTokenIssuedTo_AConfiguredAccountRemoved_IsFalseBeforeAndAfterTheDirectoryAcceptsTheName()
    {
        var accounts = new AccountBook([new Account("carol", "secret")]);
        var policy = Policy(accounts: accounts);
        accounts.Remove("carol");

        Assert.IsFalse(policy.HonoursTokenIssuedTo("carol"));
        await AcceptedByTheDirectoryAsync(policy);
        Assert.IsFalse(policy.HonoursTokenIssuedTo("carol"));
        accounts.Reset();
        accounts.Remove("carol");
        Assert.IsFalse(policy.HonoursTokenIssuedTo("carol"));
    }

    [TestMethod]
    public async Task HonoursTokenIssuedTo_AnAddedAccountAResetRemoved_IsFalseBeforeAndAfterTheDirectoryAcceptsTheName()
    {
        var accounts = new AccountBook([]);
        var policy = Policy(accounts: accounts);
        accounts.Add(new Account("carol", "secret"));
        accounts.Reset();

        Assert.IsFalse(policy.HonoursTokenIssuedTo("carol"));
        await AcceptedByTheDirectoryAsync(policy);
        Assert.IsFalse(policy.HonoursTokenIssuedTo("carol"));
    }

    [TestMethod]
    public async Task HonoursTokenIssuedTo_AnAddedAccountARestoreRemoved_IsFalseBeforeAndAfterTheDirectoryAcceptsTheName()
    {
        var accounts = new AccountBook([]);
        var policy = Policy(accounts: accounts);
        var withoutCarol = accounts.TakeSnapshot();
        accounts.Add(new Account("carol", "secret"));
        accounts.Restore(withoutCarol);

        Assert.IsFalse(policy.HonoursTokenIssuedTo("carol"));
        await AcceptedByTheDirectoryAsync(policy);
        Assert.IsFalse(policy.HonoursTokenIssuedTo("carol"));
    }

    [TestMethod]
    public async Task HonoursTokenIssuedTo_ADirectoryUsersNameAddedForOtherSchemesThenRemoved_StaysFalse()
    {
        var accounts = new AccountBook([]);
        var policy = Policy(accounts: accounts);
        await AcceptedByTheDirectoryAsync(policy);

        accounts.Add(new Account("carol", "secret") { Schemes = new HashSet<string> { "imap" } });
        Assert.IsFalse(policy.HonoursTokenIssuedTo("carol"));
        accounts.Remove("carol");
        Assert.IsFalse(policy.HonoursTokenIssuedTo("carol"));
    }

    [TestMethod]
    public async Task HonoursTokenIssuedTo_ADirectoryUsersNameAddedForThisSchemeThenRemoved_FollowsTheAccountThenIsFalse()
    {
        var accounts = new AccountBook([]);
        var policy = Policy(accounts: accounts);
        await AcceptedByTheDirectoryAsync(policy);

        accounts.Add(new Account("carol", "secret"));
        clock.Advance(OneHour);
        Assert.IsTrue(policy.HonoursTokenIssuedTo("carol"));
        accounts.Remove("carol");
        Assert.IsFalse(policy.HonoursTokenIssuedTo("carol"));
    }

    [TestMethod]
    public async Task HonoursTokenIssuedTo_AroundOneHourAfterTheLogin_IsTrueJustBeforeAndFalseAtTheHour()
    {
        var policy = Policy();
        await AcceptedByTheDirectoryAsync(policy);

        clock.Advance(OneHour - OneTick);
        Assert.IsTrue(policy.HonoursTokenIssuedTo("carol"));
        clock.Advance(OneTick);
        Assert.IsFalse(policy.HonoursTokenIssuedTo("carol"));
    }

    [TestMethod]
    public async Task HonoursTokenIssuedTo_AfterAnotherAcceptedLogin_StartsTheHourAgain()
    {
        var policy = Policy();
        await AcceptedByTheDirectoryAsync(policy);
        clock.Advance(OneHour);

        await AcceptedByTheDirectoryAsync(policy);

        clock.Advance(OneHour - OneTick);
        Assert.IsTrue(policy.HonoursTokenIssuedTo("carol"));
        clock.Advance(OneTick);
        Assert.IsFalse(policy.HonoursTokenIssuedTo("carol"));
    }

    [TestMethod]
    public async Task HonoursTokenIssuedTo_AfterARefusedLogin_NeitherEndsNorRenewsTheHour()
    {
        var policy = Policy();
        await AcceptedByTheDirectoryAsync(policy);
        clock.Advance(TimeSpan.FromMinutes(30));
        DirectoryAnswersTo(49);

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, await LogInAsync(policy));

        Assert.IsTrue(policy.HonoursTokenIssuedTo("carol"));
        clock.Advance(TimeSpan.FromMinutes(30) - AuthenticationPolicy.RefusalDelay);
        Assert.IsFalse(policy.HonoursTokenIssuedTo("carol"));
    }

    [TestMethod]
    public async Task HonoursTokenIssuedTo_PastTheCapOfRecordedNames_DropsTheNameWithTheOldestLogin()
    {
        var policy = Policy();
        for (var user = 0; user <= AcceptedDirectoryLogins.MaxNames; user++)
        {
            await AcceptedByTheDirectoryAsync(policy, $"user{user}");
            clock.Advance(TimeSpan.FromMilliseconds(1));
        }

        Assert.IsFalse(policy.HonoursTokenIssuedTo("user0"));
        Assert.IsTrue(policy.HonoursTokenIssuedTo("user1"));
        Assert.IsTrue(policy.HonoursTokenIssuedTo($"user{AcceptedDirectoryLogins.MaxNames}"));
    }
}
