using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="AuthenticationPolicy.CheckPasswordLoginAsync"/> against ADR-0032 section 5's
/// table, case by case.
/// </summary>
[TestClass]
public sealed class PasswordLoginPolicyTests
{
    private readonly ManualTimeProvider clock = new();

    private static PasswordLogin Login(string? userName, string? password, TlsSession? tlsSession) =>
        new(
            "mqtt",
            userName,
            password is null ? (ReadOnlyMemory<byte>?)null : System.Text.Encoding.UTF8.GetBytes(password),
            tlsSession);

    private async Task<PasswordLoginVerdict> CheckAsync(AuthenticationPolicy policy, PasswordLogin login)
    {
        var check = policy.CheckPasswordLoginAsync(login, CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        return await check;
    }

    [TestMethod]
    [DataRow("alice", "secret", DisplayName = "an account's own password")]
    [DataRow(null, null, DisplayName = "no credentials")]
    public async Task NoAccounts_EveryLoginIsRefused(string? userName, string? password)
    {
        var policy = PolicyFixture.Create(PolicyFixture.NoAccounts, clock);

        var verdict = await CheckAsync(policy, Login(userName, password, PolicyFixture.Tls));

        Assert.AreNotEqual(PasswordLoginVerdict.Accepted, verdict);
    }

    [TestMethod]
    public async Task RightPasswordOverTls_IsAcceptedWithoutDelay()
    {
        var policy = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock);

        var check = policy.CheckPasswordLoginAsync(Login("alice", "secret", PolicyFixture.Tls), CancellationToken.None);

        Assert.IsTrue(check.IsCompleted);
        Assert.AreEqual(PasswordLoginVerdict.Accepted, await check);
    }

    [TestMethod]
    [DataRow("alice", "wrong", DisplayName = "wrong password")]
    [DataRow("bob", "secret", DisplayName = "unknown user")]
    [DataRow("alice", "", DisplayName = "empty password")]
    [DataRow("alice", null, DisplayName = "user name without a password")]
    [DataRow("", "tok", DisplayName = "empty user name with the bearer token")]
    public async Task WrongCredentialsOverTls_AreRefusedAlike(string userName, string? password)
    {
        var policy = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock);

        var verdict = await CheckAsync(policy, Login(userName, password, PolicyFixture.Tls));

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, verdict);
    }

    [TestMethod]
    public async Task UnknownUserWithNoAccounts_GetsTheSameVerdictAsAWrongPassword()
    {
        var empty = PolicyFixture.Create(PolicyFixture.NoAccounts, clock);
        var configured = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock);

        var noAccounts = await CheckAsync(empty, Login("alice", "secret", PolicyFixture.Tls));
        var unknownUser = await CheckAsync(configured, Login("bob", "secret", PolicyFixture.Tls));
        var wrongPassword = await CheckAsync(configured, Login("alice", "wrong", PolicyFixture.Tls));

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, noAccounts);
        Assert.AreEqual(noAccounts, unknownUser);
        Assert.AreEqual(noAccounts, wrongPassword);
    }

    [TestMethod]
    public async Task RefusedCredentials_WaitTheRefusalDelayOnTheClock()
    {
        var policy = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock);

        var check = policy.CheckPasswordLoginAsync(Login("alice", "wrong", PolicyFixture.Tls), CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay - TimeSpan.FromMilliseconds(1));
        Assert.IsFalse(check.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, await check);
        Assert.AreEqual(TimeSpan.FromSeconds(1), AuthenticationPolicy.RefusalDelay);
    }

    [TestMethod]
    public async Task RefusedCredentials_DelayIsCancelledWithTheToken()
    {
        var policy = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock);
        using var cancellation = new CancellationTokenSource();

        var check = policy.CheckPasswordLoginAsync(Login("alice", "wrong", PolicyFixture.Tls), cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await check);
    }

    [TestMethod]
    [DataRow("alice", "secret", DisplayName = "right password")]
    [DataRow("alice", "wrong", DisplayName = "wrong password")]
    [DataRow(null, "secret", DisplayName = "password without a user name")]
    public async Task PasswordWithoutTls_IsRefusedUncheckedAndUndelayed(string? userName, string password)
    {
        var comparer = new CountingSecretComparer();
        var accounts = new AccountBook([new Account("alice", "secret")], comparer);
        var policy = PolicyFixture.Create(accounts, clock);

        var check = policy.CheckPasswordLoginAsync(Login(userName, password, null), CancellationToken.None);

        Assert.IsTrue(check.IsCompleted);
        Assert.AreEqual(PasswordLoginVerdict.RefusedPlaintext, await check);
        Assert.IsEmpty(comparer.Comparisons);
    }

    [TestMethod]
    public async Task PasswordWithoutTls_NoAccounts_IsRefusedAsPlaintext()
    {
        var policy = PolicyFixture.Create(PolicyFixture.NoAccounts, clock);

        Assert.AreEqual(PasswordLoginVerdict.RefusedPlaintext, await CheckAsync(policy, Login("alice", "secret", null)));
    }

    [TestMethod]
    public async Task PasswordWithoutTls_AllowPlaintextAuth_IsChecked()
    {
        var policy = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock, allowPlaintextAuth: true);

        Assert.AreEqual(PasswordLoginVerdict.Accepted, await CheckAsync(policy, Login("alice", "secret", null)));
        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, await CheckAsync(policy, Login("alice", "wrong", null)));
    }

    [TestMethod]
    [DataRow(true, DisplayName = "over TLS")]
    [DataRow(false, DisplayName = "without TLS")]
    public async Task NoUserName_IsRefusedAsAnonymous(bool overTls)
    {
        var policy = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock);

        var verdict = await CheckAsync(policy, Login(null, null, overTls ? PolicyFixture.Tls : null));

        Assert.AreEqual(PasswordLoginVerdict.RefusedAnonymous, verdict);
    }

    [TestMethod]
    public async Task NoUserNameWithPasswordOverTls_IsRefusedAsAnonymous()
    {
        var policy = PolicyFixture.Create(PolicyFixture.AliceAndToken, clock);

        Assert.AreEqual(PasswordLoginVerdict.RefusedAnonymous, await CheckAsync(policy, Login(null, "secret", PolicyFixture.Tls)));
    }

    [TestMethod]
    [DataRow(null, null, false, DisplayName = "no credentials without TLS")]
    [DataRow("alice", "wrong", false, DisplayName = "wrong password without TLS")]
    [DataRow("bob", "x", true, DisplayName = "unknown user over TLS")]
    public async Task AllowAnonymous_AcceptsEveryLoginUnchecked(string? userName, string? password, bool overTls)
    {
        var policy = PolicyFixture.Create(PolicyFixture.NoAccounts, clock, allowAnonymous: true);

        var check = policy.CheckPasswordLoginAsync(
            Login(userName, password, overTls ? PolicyFixture.Tls : null), CancellationToken.None);

        Assert.IsTrue(check.IsCompleted);
        Assert.AreEqual(PasswordLoginVerdict.Accepted, await check);
    }

    [TestMethod]
    public async Task NullLogin_Throws()
    {
        var policy = PolicyFixture.Create(PolicyFixture.NoAccounts, clock);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await policy.CheckPasswordLoginAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task CancelledToken_Throws()
    {
        var policy = PolicyFixture.Create(PolicyFixture.NoAccounts, clock);

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await policy.CheckPasswordLoginAsync(
                Login("alice", "secret", PolicyFixture.Tls), new CancellationToken(canceled: true)));
    }

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        var settings = new AuthenticationSettings(PolicyFixture.NoAccounts, false, false, AuthenticationMethods.DefaultAccepted);

        Assert.ThrowsExactly<ArgumentNullException>(() => new AuthenticationPolicy(null!, [], clock));
        Assert.ThrowsExactly<ArgumentNullException>(() => new AuthenticationPolicy(settings, null!, clock));
        Assert.ThrowsExactly<ArgumentNullException>(() => new AuthenticationPolicy(settings, [], null!));
    }
}
