using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// HTTP Basic, SASL <c>PLAIN</c> and <c>LOGIN</c> and SSH <c>password</c> logins with an LDAP account source
/// (ADR-0149, decision 2): a name the account book does not hold is judged by the directory, and a refusal
/// is answered as an unknown <c>--user-file</c> account is, after the same delay counted from the check's start.
/// </summary>
[TestClass]
public sealed class LdapOtherPasswordLoginsTests
{
    private const string Dn = "uid=carol,ou=people,dc=example,dc=com";

    private readonly ManualTimeProvider clock = new();
    private readonly ScriptedLdapDirectory directory = new();
    private readonly List<string> notes = [];

    private AuthenticationPolicy Policy()
    {
        Assert.IsTrue(LdapDirectoryUrl.TryParse("ldaps://dir.test/ou=people,dc=example,dc=com", out var url, out _));
        var source = new LdapAccountSource(new LdapAccountSourceSettings(url!), directory, clock, notes.Add);
        var settings = new AuthenticationSettings(
            PolicyFixture.AliceAndToken, false, false, AuthenticationMethods.DefaultAccepted)
        {
            LdapAccountSource = source,
        };

        return new AuthenticationPolicy(settings, [new BasicAuthenticationMethod(PolicyFixture.AliceAndToken)], clock);
    }

    // The same logins with no directory: what an unknown account costs.
    private AuthenticationPolicy PolicyWithoutDirectory() =>
        PolicyFixture.Create(PolicyFixture.AliceAndToken, clock, httpMethods: new BasicAuthenticationMethod(PolicyFixture.AliceAndToken));

    private void DirectoryAnswersTo(int bindCode)
    {
        directory.Answers.Clear();
        directory.Answers.Add(LdapTestMessages.SearchEntry(1, Dn));
        directory.Answers.Add(LdapTestMessages.SearchDone(1, 0));
        directory.Answers.Add(LdapTestMessages.BindResponse(2, bindCode));
    }

    private static Task<bool> LoginAsync(AuthenticationPolicy policy, string kind, string? user, string password)
    {
        switch (kind)
        {
            case "Basic":
                var request = PolicyFixture.Get("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));
                return policy.StartHttpConnection(PolicyFixture.Tls).JudgeAsync(request, CancellationToken.None).AsTask()
                    .ContinueWith(done => done.Result.Outcome == HttpAuthenticationOutcome.Proceed, TaskScheduler.Default);
            case "PLAIN":
                var plain = SaslExchangeRunner.Start(policy, "PLAIN", SaslExchangeRunner.Plain(string.Empty, user ?? string.Empty, password), PolicyFixture.Tls);
                return plain.BeginAsync(CancellationToken.None).AsTask()
                    .ContinueWith(done => done.Result.Outcome == SaslLoginOutcome.Accepted, TaskScheduler.Default);
            case "LOGIN":
                return LoginSaslAsync(policy, user, password);
            default:
                var ssh = new SshPasswordLogin("password", user, Encoding.UTF8.GetBytes(password));
                return policy.CheckSshPasswordLoginAsync(ssh, CancellationToken.None).AsTask()
                    .ContinueWith(done => done.Result.Outcome == SshLoginOutcome.Accepted, TaskScheduler.Default);
        }
    }

    private static async Task<bool> LoginSaslAsync(AuthenticationPolicy policy, string? user, string password)
    {
        var exchange = SaslExchangeRunner.Start(policy, "LOGIN", SaslExchangeRunner.Utf8(user ?? string.Empty), PolicyFixture.Tls);
        var prompt = await exchange.BeginAsync(CancellationToken.None);
        Assert.AreEqual(SaslLoginOutcome.Challenge, prompt.Outcome);
        var step = await exchange.ContinueAsync(SaslExchangeRunner.Utf8(password), CancellationToken.None);

        return step.Outcome == SaslLoginOutcome.Accepted;
    }

    [TestMethod]
    [DataRow("Basic")]
    [DataRow("PLAIN")]
    [DataRow("LOGIN")]
    [DataRow("SSH")]
    public async Task NameTheBookDoesNotHold_IsAcceptedWhenTheDirectoryAcceptsIt_WithoutDelay(string kind)
    {
        DirectoryAnswersTo(0);

        var login = LoginAsync(Policy(), kind, "carol", "dirpw");

        Assert.IsTrue(await login);
        Assert.AreEqual(1, directory.ConnectCount);
        Assert.IsEmpty(notes);
    }

    [TestMethod]
    [DataRow("Basic")]
    [DataRow("PLAIN")]
    [DataRow("LOGIN")]
    [DataRow("SSH")]
    public async Task NameTheBookDoesNotHold_IsRefusedWhenTheDirectoryRefusesIt_AfterTheSameDelayAsAnUnknownAccount(string kind)
    {
        DirectoryAnswersTo(49);
        var withDirectory = LoginAsync(Policy(), kind, "carol", "wrong");
        var withoutDirectory = LoginAsync(PolicyWithoutDirectory(), kind, "carol", "wrong");

        clock.Advance(AuthenticationPolicy.RefusalDelay - TimeSpan.FromMilliseconds(1));
        Assert.IsFalse(withDirectory.IsCompleted);
        Assert.IsFalse(withoutDirectory.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.IsFalse(await withoutDirectory);
        Assert.IsFalse(await withDirectory);
        Assert.AreEqual("Login refused: password carol (directory: wrong password)", notes.Single());
    }

    [TestMethod]
    [DataRow("Basic")]
    [DataRow("PLAIN")]
    [DataRow("LOGIN")]
    [DataRow("SSH")]
    public async Task DirectoryThatTimesOut_IsRefusedTheSameWay_AndNoted_WithNoDelayOnTop(string kind)
    {
        directory.Hangs = true;
        var login = LoginAsync(Policy(), kind, "carol", "pw");

        clock.Advance(TimeSpan.FromSeconds(LdapAccountSourceSettings.DefaultTimeoutSeconds));

        Assert.IsFalse(await login);
        Assert.AreEqual("Login refused: password carol (directory: timed out after 5 s)", notes.Single());
    }

    [TestMethod]
    [DataRow("Basic")]
    [DataRow("PLAIN")]
    [DataRow("LOGIN")]
    [DataRow("SSH")]
    public async Task QuickDirectoryRefusal_WaitsOnlyTheRestOfTheSecond_CountedFromTheCheckStart(string kind)
    {
        DirectoryAnswersTo(49);
        directory.Gate = new TaskCompletionSource();
        var login = LoginAsync(Policy(), kind, "carol", "wrong");

        clock.Advance(TimeSpan.FromMilliseconds(400));
        directory.Gate.SetResult();
        await WaitForRefusalDelayAsync();
        clock.Advance(TimeSpan.FromMilliseconds(599));
        Assert.IsFalse(login.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.IsFalse(await login);
    }

    [TestMethod]
    [DataRow("Basic")]
    [DataRow("PLAIN")]
    [DataRow("LOGIN")]
    [DataRow("SSH")]
    public async Task NameTheBookHolds_IsJudgedByTheBookAlone_AndNeverSentToTheDirectory(string kind)
    {
        DirectoryAnswersTo(0);
        var login = LoginAsync(Policy(), kind, "alice", "not-her-password");

        clock.Advance(AuthenticationPolicy.RefusalDelay);

        Assert.IsFalse(await login);
        Assert.AreEqual(0, directory.ConnectCount);
    }

    [TestMethod]
    [DataRow("Basic")]
    [DataRow("PLAIN")]
    [DataRow("LOGIN")]
    [DataRow("SSH")]
    public async Task NameTheBookHolds_StillLogsInWithItsOwnPassword(string kind)
    {
        var login = LoginAsync(Policy(), kind, "alice", "secret");

        Assert.IsTrue(await login);
        Assert.AreEqual(0, directory.ConnectCount);
    }

    [TestMethod]
    [DataRow("PLAIN")]
    [DataRow("LOGIN")]
    [DataRow("SSH")]
    public async Task LoginWithoutAName_IsNeverSentToTheDirectory(string kind)
    {
        DirectoryAnswersTo(0);
        var login = LoginAsync(Policy(), kind, null, "pw");

        clock.Advance(AuthenticationPolicy.RefusalDelay);

        Assert.IsFalse(await login);
        Assert.AreEqual(0, directory.ConnectCount);
    }

    // The directory has refused (its timeout timer is gone and the refusal noted) once only the refusal delay's timer is pending.
    private async Task WaitForRefusalDelayAsync()
    {
        while (notes.Count == 0 || clock.PendingTimerCount != 1)
        {
            await Task.Yield();
        }
    }
}
