using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="Account.MayLogInOn"/>, <see cref="AccountBook.ForScheme"/> and
/// <see cref="AuthenticationSettings.ForScheme"/>: an account restricted to some schemes exists
/// only in the views of those schemes, and is refused through every other view exactly as an
/// account that does not exist (ADR-0102, decision 4.3; ADR-0032, section 8).
/// </summary>
[TestClass]
public sealed class SchemeAccountViewTests
{
    private static readonly Account Alice = new("alice", "secret");

    private static readonly Account Bob = new("bob", "hunter2") { Schemes = Schemes("imap", "pop3") };

    private static readonly byte[] BobKey = SshTestKeys.Ed25519(0x42);

    private static readonly byte[] AliceKey = SshTestKeys.Ed25519(0x41);

    private readonly ManualTimeProvider clock = new();

    private static IReadOnlySet<string> Schemes(params string[] schemes) =>
        new HashSet<string>(schemes, StringComparer.OrdinalIgnoreCase);

    private static AuthenticationSettings Settings(params Account[] accounts) =>
        new(new AccountBook(accounts), false, false, AuthenticationMethods.DefaultAccepted)
        {
            AuthorizedKeys = new AuthorizedKeyBook(
                [new AuthorizedKey("alice", "ssh-ed25519", AliceKey), new AuthorizedKey("bob", "ssh-ed25519", BobKey)]),
        };

    // Alice unrestricted and Bob restricted to imap and pop3, seen through scheme's view.
    private static AuthenticationSettings RestrictedView(string scheme) => Settings(Alice, Bob, new Account(string.Empty, "tok")).ForScheme(scheme);

    // Bob not configured at all: what a refusal through another scheme's view must equal.
    private static AuthenticationSettings WithoutBob() => Settings(Alice, new Account(string.Empty, "tok"));

    private AuthenticationPolicy Policy(AuthenticationSettings settings) =>
        new(settings, [new BasicAuthenticationMethod(settings.Accounts)], clock);

    private static PasswordLogin BobPasswordLogin(string scheme) =>
        new(scheme, "bob", Encoding.UTF8.GetBytes("hunter2"), PolicyFixture.Tls);

    private async Task<(bool WasImmediate, T Result)> SettleAsync<T>(ValueTask<T> pending)
    {
        var wasImmediate = pending.IsCompleted;
        var task = pending.AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        return (wasImmediate, await task);
    }

    [TestMethod]
    [DataRow("imap", DisplayName = "a scheme named")]
    [DataRow("IMAPS", DisplayName = "the TLS twin of a scheme named, in another case")]
    [DataRow("pop3s", DisplayName = "the other scheme's TLS twin")]
    public void MayLogInOn_ASchemeNamedOrItsTlsTwin_IsTrue(string scheme)
    {
        Assert.IsTrue(Bob.MayLogInOn(scheme));
    }

    [TestMethod]
    [DataRow("smtp")]
    [DataRow("http")]
    [DataRow("imapss")]
    [DataRow("ima")]
    public void MayLogInOn_ASchemeNotNamed_IsFalse(string scheme)
    {
        Assert.IsFalse(Bob.MayLogInOn(scheme));
    }

    [TestMethod]
    public void MayLogInOn_TheTwinWithoutTheFinalS_IsTrue()
    {
        var account = new Account("carol", "pw") { Schemes = Schemes("wss") };

        Assert.IsTrue(account.MayLogInOn("ws"));
        Assert.IsTrue(account.MayLogInOn("wss"));
        Assert.IsFalse(account.MayLogInOn("w"));
    }

    [TestMethod]
    [DataRow("http")]
    [DataRow("smb")]
    [DataRow("anything")]
    public void MayLogInOn_AnUnrestrictedAccount_IsTrueForEveryScheme(string scheme)
    {
        Assert.IsTrue(Alice.MayLogInOn(scheme));
    }

    [TestMethod]
    public void MayLogInOn_NullScheme_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Bob.MayLogInOn(null!));
    }

    [TestMethod]
    [DataRow("smtp", true)]
    [DataRow("smtps", true)]
    [DataRow("imaps", true)]
    [DataRow("pop3", true)]
    [DataRow("ftp", false)]
    [DataRow("https", false)]
    public void MailSchemes_AdmitTheMailSchemesAndTheirTwinsOnly(string scheme, bool isAdmitted)
    {
        var mailbox = new Account("ann@example.com", "pw") { Schemes = Account.MailSchemes };

        Assert.AreEqual(isAdmitted, mailbox.MayLogInOn(scheme));
    }

    [TestMethod]
    public void ForScheme_EveryAccountUnrestricted_IsTheSameBookAndSettings()
    {
        var settings = Settings(Alice, new Account(string.Empty, "tok"));

        Assert.AreSame(settings, settings.ForScheme("smtp"));
        Assert.AreSame(settings.Accounts, settings.Accounts.ForScheme("smtp"));
    }

    [TestMethod]
    public void ForScheme_ASchemeTheRestrictedAccountNames_IsTheSameSettings()
    {
        var settings = Settings(Alice, Bob);

        Assert.AreSame(settings, settings.ForScheme("imaps"));
    }

    [TestMethod]
    public void ForScheme_NullScheme_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Settings(Alice).ForScheme(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new AccountBook([Alice]).ForScheme(null!));
    }

    [TestMethod]
    public async Task PasswordLogin_RestrictedAccount_IsAcceptedThroughTheImapView()
    {
        var (wasImmediate, verdict) = await SettleAsync(
            Policy(RestrictedView("imap")).CheckPasswordLoginAsync(BobPasswordLogin("imap"), CancellationToken.None));

        Assert.IsTrue(wasImmediate);
        Assert.AreEqual(PasswordLoginVerdict.Accepted, verdict);
    }

    [TestMethod]
    [DataRow("smtp")]
    [DataRow("http")]
    public async Task PasswordLogin_RestrictedAccount_IsRefusedThroughAnotherViewAsAnUnknownAccount(string scheme)
    {
        var (viewImmediate, viewVerdict) = await SettleAsync(
            Policy(RestrictedView(scheme)).CheckPasswordLoginAsync(BobPasswordLogin(scheme), CancellationToken.None));
        var (unknownImmediate, unknownVerdict) = await SettleAsync(
            Policy(WithoutBob()).CheckPasswordLoginAsync(BobPasswordLogin(scheme), CancellationToken.None));

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, viewVerdict);
        Assert.AreEqual(unknownVerdict, viewVerdict);
        Assert.IsFalse(viewImmediate);
        Assert.AreEqual(unknownImmediate, viewImmediate);
    }

    [TestMethod]
    [DataRow("smtp")]
    [DataRow("http")]
    [DataRow("imap")]
    public async Task PasswordLogin_UnrestrictedAccount_IsAcceptedThroughEveryView(string scheme)
    {
        var login = new PasswordLogin(scheme, "alice", Encoding.UTF8.GetBytes("secret"), PolicyFixture.Tls);

        var (_, verdict) = await SettleAsync(Policy(RestrictedView(scheme)).CheckPasswordLoginAsync(login, CancellationToken.None));

        Assert.AreEqual(PasswordLoginVerdict.Accepted, verdict);
    }

    [TestMethod]
    public void ForScheme_UnrestrictedBearerTokenAccount_StaysInEveryView()
    {
        var view = RestrictedView("smtp");

        Assert.IsTrue(view.Accounts.CheckBearerToken("tok"u8));
        Assert.IsTrue(view.Accounts.HasAccounts);
    }

    [TestMethod]
    [DataRow("imap", SaslLoginOutcome.Accepted, DisplayName = "imap view")]
    [DataRow("smtp", SaslLoginOutcome.RefusedCredentials, DisplayName = "smtp view")]
    public async Task SaslPlain_RestrictedAccount_IsAcceptedOnlyThroughItsSchemesViews(string scheme, SaslLoginOutcome outcome)
    {
        var step = await RunPlainAsync(RestrictedView(scheme), scheme);

        Assert.AreEqual(outcome, step.Outcome);
    }

    [TestMethod]
    [DataRow("PLAIN")]
    [DataRow("CRAM-MD5")]
    [DataRow("DIGEST-MD5")]
    [DataRow("SCRAM-SHA-256")]
    [DataRow("NTLM")]
    public async Task Sasl_RestrictedAccountThroughTheSmtpView_IsAnsweredAsAnUnknownAccount(string mechanism)
    {
        var accepted = new HashSet<AuthenticationMethod>(AuthenticationMethods.DefaultAccepted)
        {
            AuthenticationMethod.ScramSha256,
        };
        var view = RestrictedView("smtp") with { AcceptedMethods = accepted };
        var unknown = WithoutBob() with { AcceptedMethods = accepted };

        var viewStart = PolicyFixture.CreateWithFixedNonces(view.Accounts, clock, acceptedMethods: accepted)
            .StartSaslExchange(new SaslExchangeStart("smtp", mechanism, null, PolicyFixture.Tls));
        var unknownStart = PolicyFixture.CreateWithFixedNonces(unknown.Accounts, clock, acceptedMethods: accepted)
            .StartSaslExchange(new SaslExchangeStart("smtp", mechanism, null, PolicyFixture.Tls));
        var viewFirst = await new SaslExchangeRunner(clock).RunAsync(viewStart);
        var unknownFirst = await new SaslExchangeRunner(clock).RunAsync(unknownStart);

        Assert.AreEqual(unknownFirst[0].Outcome, viewFirst[0].Outcome);
        Assert.IsTrue(view.Accounts.FindChallengeResponseAccount("bob").AccountName is null);
        Assert.IsTrue(view.Accounts.FindNtlmAccount("bob").AccountName is null);
        Assert.IsTrue(view.Accounts.FindScramCredentials(ScramHash.Sha256, "bob").AccountName is null);
        Assert.IsTrue(view.Accounts.FindScramCredentials(ScramHash.Sha1, "bob").AccountName is null);
        Assert.IsTrue(view.Accounts.FindAwsSigV4Account("bob").AccountName is null);
        Assert.IsTrue(view.Accounts.FindDigestAccount("bob").AccountName is null);
        Assert.IsFalse(view.Accounts.HasNamedAccount("bob"));
    }

    [TestMethod]
    public void View_KeepsEveryDerivedSecretOfTheAdmittedAccounts()
    {
        var full = Settings(Alice, Bob).Accounts;
        var view = full.ForScheme("smtp");

        Assert.AreSame(full.FindNtlmAccount("alice"), view.FindNtlmAccount("alice"));
        Assert.AreSame(full.FindChallengeResponseAccount("alice"), view.FindChallengeResponseAccount("alice"));
        Assert.AreSame(full.FindAwsSigV4Account("alice"), view.FindAwsSigV4Account("alice"));
        Assert.AreSame(full.FindDigestAccount("alice"), view.FindDigestAccount("alice"));
        Assert.AreSame(full.FindScramCredentials(ScramHash.Sha256, "alice"), view.FindScramCredentials(ScramHash.Sha256, "alice"));
        Assert.IsTrue(view.HasNamedAccount("alice"));
        Assert.IsTrue(view.ForScheme("smtp") == view);
    }

    [TestMethod]
    public async Task SaslPlain_RestrictedAccountThroughTheSmtpView_HasTheSameNoteAndDelayAsAnUnknownAccount()
    {
        var viewStep = await RunPlainAsync(RestrictedView("smtp"), "smtp");
        var unknownStep = await RunPlainAsync(WithoutBob(), "smtp");

        Assert.AreEqual(SaslLoginOutcome.RefusedCredentials, viewStep.Outcome);
        Assert.AreEqual(unknownStep.Outcome, viewStep.Outcome);
        Assert.AreEqual(unknownStep.AccountName, viewStep.AccountName);
        Assert.AreEqual(unknownStep.CheckedLogin, viewStep.CheckedLogin);
        Assert.AreEqual(unknownStep.RefusalNote, viewStep.RefusalNote);
    }

    private async Task<SaslLoginStep> RunPlainAsync(AuthenticationSettings settings, string scheme)
    {
        var exchange = Policy(settings).StartSaslExchange(new SaslExchangeStart(
            scheme, "PLAIN", SaslExchangeRunner.Plain(string.Empty, "bob", "hunter2"), PolicyFixture.Tls));
        var begun = exchange.BeginAsync(CancellationToken.None);
        Assert.AreEqual(scheme == "imap", begun.IsCompleted);
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        return await begun;
    }

    [TestMethod]
    [DataRow("imap", HttpAuthenticationOutcome.Proceed, DisplayName = "imap view")]
    [DataRow("http", HttpAuthenticationOutcome.Challenge, DisplayName = "http view")]
    public async Task HttpBasic_RestrictedAccount_IsServedOnlyThroughItsSchemesViews(string scheme, HttpAuthenticationOutcome outcome)
    {
        var verdict = await JudgeBobBasicAsync(RestrictedView(scheme));

        Assert.AreEqual(outcome, verdict.Outcome);
    }

    [TestMethod]
    public async Task HttpBasic_RestrictedAccountThroughTheHttpView_IsAnsweredAsAnUnknownAccount()
    {
        var viewVerdict = await JudgeBobBasicAsync(RestrictedView("http"));
        var unknownVerdict = await JudgeBobBasicAsync(WithoutBob());

        Assert.AreEqual(unknownVerdict.Outcome, viewVerdict.Outcome);
        CollectionAssert.AreEqual(unknownVerdict.WwwAuthenticateValues.ToArray(), viewVerdict.WwwAuthenticateValues.ToArray());
        Assert.AreEqual(unknownVerdict.AccountName, viewVerdict.AccountName);
        Assert.AreEqual(new CheckedLogin("Basic", "bob", false), viewVerdict.CheckedLogin);
        Assert.AreEqual(unknownVerdict.CheckedLogin, viewVerdict.CheckedLogin);
    }

    private async Task<HttpAuthenticationVerdict> JudgeBobBasicAsync(AuthenticationSettings settings)
    {
        var request = PolicyFixture.Get("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("bob:hunter2")));
        var judgement = Policy(settings).StartHttpConnection(PolicyFixture.Tls).JudgeAsync(request, CancellationToken.None);
        var wasImmediate = judgement.IsCompleted;
        var task = judgement.AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);
        var verdict = await task;
        Assert.AreEqual(verdict.Outcome == HttpAuthenticationOutcome.Proceed, wasImmediate);

        return verdict;
    }

    [TestMethod]
    [DataRow("imap", SshLoginOutcome.Accepted, DisplayName = "imap view")]
    [DataRow("sftp", SshLoginOutcome.Refused, DisplayName = "sftp view")]
    public async Task SshPublicKey_RestrictedAccount_IsAcceptedOnlyThroughItsSchemesViews(string scheme, SshLoginOutcome outcome)
    {
        var (_, verdict) = await SettleAsync(Policy(RestrictedView(scheme)).CheckSshPublicKeyLoginAsync(
            new SshPublicKeyLogin("bob", "ssh-ed25519", BobKey, SshPublicKeyProof.ValidSignature), CancellationToken.None));

        Assert.AreEqual(outcome, verdict.Outcome);
    }

    [TestMethod]
    public async Task SshPublicKey_RestrictedAccountThroughTheSftpView_IsAnsweredAsAnUnknownUser()
    {
        var unknownKeys = WithoutBob() with
        {
            AuthorizedKeys = new AuthorizedKeyBook([new AuthorizedKey("alice", "ssh-ed25519", AliceKey)]),
        };
        var login = new SshPublicKeyLogin("bob", "ssh-ed25519", BobKey, SshPublicKeyProof.ValidSignature);

        var (viewImmediate, viewVerdict) = await SettleAsync(Policy(RestrictedView("sftp")).CheckSshPublicKeyLoginAsync(login, CancellationToken.None));
        var (unknownImmediate, unknownVerdict) = await SettleAsync(Policy(unknownKeys).CheckSshPublicKeyLoginAsync(login, CancellationToken.None));
        var query = Policy(RestrictedView("sftp")).CheckSshPublicKeyLoginAsync(
            login with { Proof = SshPublicKeyProof.None }, CancellationToken.None);

        Assert.AreEqual(unknownVerdict, viewVerdict);
        Assert.IsFalse(viewImmediate);
        Assert.AreEqual(unknownImmediate, viewImmediate);
        Assert.AreEqual(SshLoginOutcome.Refused, (await query).Outcome);
    }

    [TestMethod]
    public async Task SshPublicKey_UnrestrictedAccount_IsAcceptedThroughTheSftpView()
    {
        var (_, verdict) = await SettleAsync(Policy(RestrictedView("sftp")).CheckSshPublicKeyLoginAsync(
            new SshPublicKeyLogin("alice", "ssh-ed25519", AliceKey, SshPublicKeyProof.ValidSignature), CancellationToken.None));

        Assert.AreEqual(SshLoginOutcome.Accepted, verdict.Outcome);
    }

    [TestMethod]
    public void ForScheme_NoKeyOfARefusedAccount_KeepsTheSameKeyBook()
    {
        var settings = Settings(Alice, Bob) with
        {
            AuthorizedKeys = new AuthorizedKeyBook([new AuthorizedKey("alice", "ssh-ed25519", AliceKey)]),
        };

        var view = settings.ForScheme("sftp");

        Assert.AreSame(settings.AuthorizedKeys, view.AuthorizedKeys);
        Assert.AreNotSame(settings.Accounts, view.Accounts);
    }
}
