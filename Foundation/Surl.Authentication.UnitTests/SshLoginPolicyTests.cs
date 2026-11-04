using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="AuthenticationPolicy"/> as the <see cref="ISshAuthenticationPolicy"/>, against
/// ADR-0051 sections 6 and 7.
/// </summary>
[TestClass]
public sealed class SshLoginPolicyTests
{
    private static readonly byte[] AliceKey = SshTestKeys.Ed25519(0x01);

    private static readonly byte[] OtherKey = SshTestKeys.Ed25519(0x02);

    private readonly ManualTimeProvider clock = new();

    private AuthenticationPolicy Create(AccountBook accounts, bool allowAnonymous = false, AuthorizedKeyBook? keys = null) =>
        new(
            new AuthenticationSettings(accounts, allowAnonymous, false, AuthenticationMethods.DefaultAccepted)
            {
                AuthorizedKeys = keys ?? new AuthorizedKeyBook([new AuthorizedKey("alice", "ssh-ed25519", AliceKey)]),
            },
            [],
            clock);

    private static SshPasswordLogin Password(string? userName, string password, string method = "password") =>
        new(method, userName, Encoding.UTF8.GetBytes(password));

    private static SshPublicKeyLogin PublicKey(string? userName, byte[] blob, SshPublicKeyProof proof) =>
        new(userName, "ssh-ed25519", blob, proof);

    private async Task<SshLoginVerdict> CheckAsync(ValueTask<SshLoginVerdict> check)
    {
        var task = check.AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        return await task;
    }

    [TestMethod]
    [DataRow("password", DisplayName = "password")]
    [DataRow("keyboard-interactive", DisplayName = "keyboard-interactive")]
    public async Task CheckSshPasswordLoginAsync_RightPasswordWithoutTlsOrPlaintextAuth_IsAcceptedUndelayedWithANote(string method)
    {
        var policy = Create(PolicyFixture.AliceAndToken);

        var check = policy.CheckSshPasswordLoginAsync(Password("alice", "secret", method), CancellationToken.None);

        Assert.IsTrue(check.IsCompleted);
        var verdict = await check;
        Assert.AreEqual(SshLoginOutcome.Accepted, verdict.Outcome);
        Assert.AreEqual("alice", verdict.AccountName);
        Assert.AreEqual(new CheckedLogin(method, "alice", true), verdict.CheckedLogin);
        Assert.AreEqual($"Login accepted: {method} alice", verdict.CheckedLogin!.Note);
    }

    [TestMethod]
    public async Task CheckSshPasswordLoginAsync_WrongPasswordUnknownUserAndNoAccounts_AreRefusedAlike()
    {
        var configured = Create(PolicyFixture.AliceAndToken);
        var empty = Create(PolicyFixture.NoAccounts);

        var wrongPassword = await CheckAsync(configured.CheckSshPasswordLoginAsync(Password("alice", "wrong"), CancellationToken.None));
        var unknownUser = await CheckAsync(configured.CheckSshPasswordLoginAsync(Password("alice2", "secret"), CancellationToken.None));
        var noAccounts = await CheckAsync(empty.CheckSshPasswordLoginAsync(Password("alice", "secret"), CancellationToken.None));

        Assert.AreEqual(new SshLoginVerdict(SshLoginOutcome.Refused, null, new CheckedLogin("password", "alice", false)), wrongPassword);
        Assert.AreEqual(new SshLoginVerdict(SshLoginOutcome.Refused, null, new CheckedLogin("password", "alice2", false)), unknownUser);
        Assert.AreEqual(wrongPassword, noAccounts);
    }

    [TestMethod]
    [DataRow("", "tok", DisplayName = "empty user name with the bearer token")]
    [DataRow(null, "secret", DisplayName = "a user name that is not UTF-8")]
    public async Task CheckSshPasswordLoginAsync_NoNamedAccount_IsRefused(string? userName, string password)
    {
        var policy = Create(PolicyFixture.AliceAndToken);

        var verdict = await CheckAsync(policy.CheckSshPasswordLoginAsync(Password(userName, password), CancellationToken.None));

        Assert.AreEqual(SshLoginOutcome.Refused, verdict.Outcome);
        Assert.AreEqual(new CheckedLogin("password", userName, false), verdict.CheckedLogin);
    }

    [TestMethod]
    public async Task CheckSshPasswordLoginAsync_UnknownUser_CostsOneComparisonAsAWrongPasswordDoes()
    {
        var comparer = new CountingSecretComparer();
        var policy = Create(new AccountBook([new Account("alice", "secret")], comparer));

        await CheckAsync(policy.CheckSshPasswordLoginAsync(Password("bob", "secret"), CancellationToken.None));
        await CheckAsync(policy.CheckSshPasswordLoginAsync(Password("alice", "wrong"), CancellationToken.None));

        Assert.HasCount(2, comparer.Comparisons);
        Assert.AreEqual(comparer.Comparisons[0], comparer.Comparisons[1]);
    }

    [TestMethod]
    public async Task CheckSshPasswordLoginAsync_Refused_WaitsTheRefusalDelayOnTheClock()
    {
        var policy = Create(PolicyFixture.AliceAndToken);

        var check = policy.CheckSshPasswordLoginAsync(Password("alice", "wrong"), CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay - TimeSpan.FromMilliseconds(1));
        Assert.IsFalse(check.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.AreEqual(SshLoginOutcome.Refused, (await check).Outcome);
    }

    [TestMethod]
    public async Task CheckSshPasswordLoginAsync_RefusalDelay_IsCancelledWithTheToken()
    {
        var policy = Create(PolicyFixture.AliceAndToken);
        using var cancellation = new CancellationTokenSource();

        var check = policy.CheckSshPasswordLoginAsync(Password("alice", "wrong"), cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await check);
    }

    [TestMethod]
    public async Task CheckSshPasswordLoginAsync_NoteAndVerdict_NeverHoldThePassword()
    {
        var policy = Create(PolicyFixture.AliceAndToken);

        var accepted = await CheckAsync(policy.CheckSshPasswordLoginAsync(Password("alice", "secret"), CancellationToken.None));
        var refused = await CheckAsync(policy.CheckSshPasswordLoginAsync(Password("alice", "hunter2"), CancellationToken.None));

        Assert.DoesNotContain("secret", accepted.ToString());
        Assert.DoesNotContain("secret", accepted.CheckedLogin!.Note);
        Assert.DoesNotContain("hunter2", refused.ToString());
        Assert.DoesNotContain("hunter2", refused.CheckedLogin!.Note);
    }

    [TestMethod]
    public async Task CheckSshPublicKeyLoginAsync_AuthorizedSignedKey_IsAcceptedUndelayedWithANote()
    {
        var policy = Create(PolicyFixture.NoAccounts);

        var check = policy.CheckSshPublicKeyLoginAsync(
            PublicKey("alice", AliceKey, SshPublicKeyProof.ValidSignature), CancellationToken.None);

        Assert.IsTrue(check.IsCompleted);
        Assert.AreEqual(
            new SshLoginVerdict(SshLoginOutcome.Accepted, "alice", new CheckedLogin("publickey", "alice", true)),
            await check);
    }

    [TestMethod]
    [DataRow("bob", DisplayName = "another user")]
    [DataRow("ALICE", DisplayName = "the user in another case")]
    [DataRow(null, DisplayName = "a user name that is not UTF-8")]
    public async Task CheckSshPublicKeyLoginAsync_AnotherUsersKey_IsRefusedAfterTheDelayWithANote(string? userName)
    {
        var policy = Create(PolicyFixture.NoAccounts);

        var check = policy.CheckSshPublicKeyLoginAsync(
            PublicKey(userName, AliceKey, SshPublicKeyProof.ValidSignature), CancellationToken.None).AsTask();
        Assert.IsFalse(check.IsCompleted);
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        Assert.AreEqual(
            new SshLoginVerdict(SshLoginOutcome.Refused, null, new CheckedLogin("publickey", userName, false)),
            await check);
    }

    [TestMethod]
    public async Task CheckSshPublicKeyLoginAsync_UnauthorizedKeyAndNoKeysConfigured_AreRefusedAlike()
    {
        var configured = Create(PolicyFixture.NoAccounts);
        var empty = Create(PolicyFixture.NoAccounts, keys: AuthorizedKeyBook.Empty);

        var otherKey = await CheckAsync(configured.CheckSshPublicKeyLoginAsync(
            PublicKey("alice", OtherKey, SshPublicKeyProof.ValidSignature), CancellationToken.None));
        var noKeys = await CheckAsync(empty.CheckSshPublicKeyLoginAsync(
            PublicKey("alice", AliceKey, SshPublicKeyProof.ValidSignature), CancellationToken.None));

        Assert.AreEqual(SshLoginOutcome.Refused, otherKey.Outcome);
        Assert.AreEqual(otherKey, noKeys);
    }

    [TestMethod]
    public async Task CheckSshPublicKeyLoginAsync_AuthorizedKeyWithAnInvalidSignature_IsRefusedAfterTheDelay()
    {
        var policy = Create(PolicyFixture.NoAccounts);

        var check = policy.CheckSshPublicKeyLoginAsync(
            PublicKey("alice", AliceKey, SshPublicKeyProof.InvalidSignature), CancellationToken.None).AsTask();
        Assert.IsFalse(check.IsCompleted);
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        Assert.AreEqual(
            new SshLoginVerdict(SshLoginOutcome.Refused, null, new CheckedLogin("publickey", "alice", false)),
            await check);
    }

    [TestMethod]
    [DataRow("alice", true, DisplayName = "the user's own key")]
    [DataRow("bob", false, DisplayName = "another user")]
    public async Task CheckSshPublicKeyLoginAsync_Query_IsAnsweredUndelayedWithoutANote(string userName, bool isAuthorized)
    {
        var policy = Create(PolicyFixture.NoAccounts);

        var check = policy.CheckSshPublicKeyLoginAsync(
            PublicKey(userName, AliceKey, SshPublicKeyProof.None), CancellationToken.None);

        Assert.IsTrue(check.IsCompleted);
        var expected = isAuthorized ? SshLoginOutcome.KeyAcceptable : SshLoginOutcome.Refused;
        Assert.AreEqual(new SshLoginVerdict(expected, null, null), await check);
    }

    [TestMethod]
    public void CheckSshNoneLogin_WithoutAllowAnonymous_IsRefusedWithoutANote()
    {
        var policy = Create(PolicyFixture.AliceAndToken);

        var verdict = policy.CheckSshNoneLogin(new SshNoneLogin("alice"));

        Assert.AreEqual(new SshLoginVerdict(SshLoginOutcome.Refused, null, null), verdict);
    }

    [TestMethod]
    public async Task AllowAnonymous_EveryLogin_IsAcceptedUncheckedUndelayedWithoutANote()
    {
        var policy = Create(PolicyFixture.NoAccounts, allowAnonymous: true, keys: AuthorizedKeyBook.Empty);
        var acceptedUnchecked = new SshLoginVerdict(SshLoginOutcome.AcceptedUnchecked, null, null);

        var none = policy.CheckSshNoneLogin(new SshNoneLogin("anyone"));
        var password = policy.CheckSshPasswordLoginAsync(Password("anyone", "anything"), CancellationToken.None);
        var query = policy.CheckSshPublicKeyLoginAsync(PublicKey("anyone", OtherKey, SshPublicKeyProof.None), CancellationToken.None);
        var signed = policy.CheckSshPublicKeyLoginAsync(PublicKey("anyone", OtherKey, SshPublicKeyProof.ValidSignature), CancellationToken.None);
        var badSignature = policy.CheckSshPublicKeyLoginAsync(PublicKey("anyone", OtherKey, SshPublicKeyProof.InvalidSignature), CancellationToken.None);

        Assert.AreEqual(acceptedUnchecked, none);
        Assert.IsTrue(password.IsCompleted && signed.IsCompleted && badSignature.IsCompleted && query.IsCompleted);
        Assert.AreEqual(acceptedUnchecked, await password);
        Assert.AreEqual(new SshLoginVerdict(SshLoginOutcome.KeyAcceptable, null, null), await query);
        Assert.AreEqual(acceptedUnchecked, await signed);
        Assert.AreEqual(acceptedUnchecked, await badSignature);
    }

    [TestMethod]
    public async Task SshChecks_NullLoginOrCancelledToken_Throw()
    {
        var policy = Create(PolicyFixture.AliceAndToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Assert.ThrowsExactly<ArgumentNullException>(() => policy.CheckSshNoneLogin(null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await policy.CheckSshPasswordLoginAsync(null!, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await policy.CheckSshPublicKeyLoginAsync(null!, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await policy.CheckSshPasswordLoginAsync(Password("alice", "secret"), cancellation.Token));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await policy.CheckSshPublicKeyLoginAsync(PublicKey("alice", AliceKey, SshPublicKeyProof.None), cancellation.Token));
    }

    [TestMethod]
    public void AuthenticationSettings_WithoutAuthorizedKeys_HasTheEmptyBook()
    {
        var settings = new AuthenticationSettings(PolicyFixture.NoAccounts, false, false, AuthenticationMethods.DefaultAccepted);

        Assert.AreSame(AuthorizedKeyBook.Empty, settings.AuthorizedKeys);
    }
}
