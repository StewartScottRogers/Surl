using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="AuthenticationPolicy"/> as the <see cref="ISmbAuthenticationPolicy"/>, against
/// ADR-0073 decision 3, with [MS-NLMP] section 4.2.2's NTLMv1 example: user <c>User</c>, domain
/// <c>Domain</c>, password <c>Password</c>, server challenge <c>0123456789abcdef</c>.
/// </summary>
[TestClass]
public sealed class SmbLoginPolicyTests
{
    private static readonly byte[] ServerChallenge = Convert.FromHexString("0123456789ABCDEF");

    // Section 4.2.2.2.2's LMv1 response and section 4.2.2.2.1's NTLMv1 response.
    private static readonly byte[] LmResponse = Convert.FromHexString("98DEF7B87F88AA5DAFE2DF779688A172DEF11C7D5CCDEF13");

    private static readonly byte[] NtResponse = Convert.FromHexString("67C43011F30298A2AD35ECE64F16331C44BDBED927841F94");

    private static readonly IReadOnlySet<AuthenticationMethod> AcceptsNtlmV1 = new HashSet<AuthenticationMethod>
    {
        AuthenticationMethod.NtlmV1,
    };

    private static readonly AccountBook UserAccount = new([new Account("User", "Password")]);

    private readonly ManualTimeProvider clock = new();

    private AuthenticationPolicy Create(
        AccountBook accounts, IReadOnlySet<AuthenticationMethod>? acceptedMethods = null, bool allowAnonymous = false) =>
        PolicyFixture.Create(accounts, clock, allowAnonymous, acceptedMethods: acceptedMethods ?? AcceptsNtlmV1);

    private static SmbNtlmV1Login Login(
        string userName = "User",
        byte[]? ntResponse = null,
        byte[]? lmResponse = null,
        TlsSession? tlsSession = null,
        string domainName = "Domain") =>
        new(userName, domainName, ServerChallenge, lmResponse ?? LmResponse, ntResponse ?? NtResponse, tlsSession);

    private async Task<SmbLoginVerdict> CheckAsync(ValueTask<SmbLoginVerdict> check)
    {
        var task = check.AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        return await task;
    }

    private static byte[] Flipped(byte[] response, int index)
    {
        var copy = (byte[])response.Clone();
        copy[index] ^= 0x01;

        return copy;
    }

    [TestMethod]
    [DataRow(false, DisplayName = "smb, no TLS")]
    [DataRow(true, DisplayName = "smbs, TLS")]
    public async Task CheckSmbNtlmV1LoginAsync_SpecificationResponsesWithNtlmV1Accepted_IsAcceptedUndelayedWithANote(bool isTls)
    {
        var policy = Create(UserAccount);

        var check = policy.CheckSmbNtlmV1LoginAsync(
            Login(tlsSession: isTls ? PolicyFixture.Tls : null), CancellationToken.None);

        Assert.IsTrue(check.IsCompleted);
        var verdict = await check;
        Assert.AreEqual(new SmbLoginVerdict(SmbLoginOutcome.Accepted, "User", new CheckedLogin("ntlmv1", "User", true)), verdict);
        Assert.AreEqual("Login accepted: ntlmv1 User", verdict.CheckedLogin!.Note);
    }

    [TestMethod]
    [DataRow("Domain", DisplayName = "the specification's domain")]
    [DataRow("DOM", DisplayName = "another domain")]
    [DataRow("", DisplayName = "no domain")]
    public async Task CheckSmbNtlmV1LoginAsync_AnyDomain_IsNotMatched(string domainName)
    {
        var policy = Create(UserAccount);

        var verdict = await policy.CheckSmbNtlmV1LoginAsync(Login(domainName: domainName), CancellationToken.None);

        Assert.AreEqual(SmbLoginOutcome.Accepted, verdict.Outcome);
    }

    [TestMethod]
    public async Task CheckSmbNtlmV1LoginAsync_WrongOrEmptyLmResponse_IsIgnored()
    {
        var policy = Create(UserAccount);

        var wrongLm = await policy.CheckSmbNtlmV1LoginAsync(Login(lmResponse: Flipped(LmResponse, 0)), CancellationToken.None);
        var noLm = await policy.CheckSmbNtlmV1LoginAsync(Login(lmResponse: []), CancellationToken.None);

        Assert.AreEqual(SmbLoginOutcome.Accepted, wrongLm.Outcome);
        Assert.AreEqual(SmbLoginOutcome.Accepted, noLm.Outcome);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "first byte")]
    [DataRow(23, DisplayName = "last byte")]
    public async Task CheckSmbNtlmV1LoginAsync_WrongNtResponse_IsRefusedWithANote(int flippedIndex)
    {
        var policy = Create(UserAccount);

        var verdict = await CheckAsync(policy.CheckSmbNtlmV1LoginAsync(
            Login(ntResponse: Flipped(NtResponse, flippedIndex)), CancellationToken.None));

        Assert.AreEqual(new SmbLoginVerdict(SmbLoginOutcome.Refused, null, new CheckedLogin("ntlmv1", "User", false)), verdict);
        Assert.AreEqual("Login refused: ntlmv1 User", verdict.CheckedLogin!.Note);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "empty")]
    [DataRow(16, DisplayName = "16 bytes")]
    [DataRow(25, DisplayName = "25 bytes")]
    public async Task CheckSmbNtlmV1LoginAsync_NtResponseNot24Bytes_IsRefusedAsAWrongCredential(int length)
    {
        var policy = Create(UserAccount);
        var response = new byte[length];
        NtResponse.AsSpan(0, Math.Min(length, NtResponse.Length)).CopyTo(response);

        var verdict = await CheckAsync(policy.CheckSmbNtlmV1LoginAsync(Login(ntResponse: response), CancellationToken.None));

        Assert.AreEqual(new SmbLoginVerdict(SmbLoginOutcome.Refused, null, new CheckedLogin("ntlmv1", "User", false)), verdict);
    }

    [TestMethod]
    public async Task CheckSmbNtlmV1LoginAsync_UnknownUserWrongCaseAndNoAccounts_AreRefusedAlike()
    {
        var configured = Create(UserAccount);
        var empty = Create(PolicyFixture.NoAccounts);

        var unknownUser = await CheckAsync(configured.CheckSmbNtlmV1LoginAsync(Login(userName: "Other"), CancellationToken.None));
        var wrongCase = await CheckAsync(configured.CheckSmbNtlmV1LoginAsync(Login(userName: "user"), CancellationToken.None));
        var noAccounts = await CheckAsync(empty.CheckSmbNtlmV1LoginAsync(Login(), CancellationToken.None));

        Assert.AreEqual(new SmbLoginVerdict(SmbLoginOutcome.Refused, null, new CheckedLogin("ntlmv1", "Other", false)), unknownUser);
        Assert.AreEqual(new SmbLoginVerdict(SmbLoginOutcome.Refused, null, new CheckedLogin("ntlmv1", "user", false)), wrongCase);
        Assert.AreEqual(new SmbLoginVerdict(SmbLoginOutcome.Refused, null, new CheckedLogin("ntlmv1", "User", false)), noAccounts);
    }

    [TestMethod]
    public async Task CheckSmbNtlmV1LoginAsync_EmptyUserNameWithTheBearerTokensPassword_IsRefused()
    {
        var policy = Create(new AccountBook([new Account(string.Empty, "Password")]));

        var verdict = await CheckAsync(policy.CheckSmbNtlmV1LoginAsync(Login(userName: string.Empty), CancellationToken.None));

        Assert.AreEqual(SmbLoginOutcome.Refused, verdict.Outcome);
    }

    [TestMethod]
    public async Task CheckSmbNtlmV1LoginAsync_UnknownUser_CostsOneComparisonAsAWrongResponseDoes()
    {
        var comparer = new CountingSecretComparer();
        var policy = Create(new AccountBook([new Account("User", "Password")], comparer));

        await CheckAsync(policy.CheckSmbNtlmV1LoginAsync(Login(userName: "Other"), CancellationToken.None));
        await CheckAsync(policy.CheckSmbNtlmV1LoginAsync(Login(ntResponse: Flipped(NtResponse, 0)), CancellationToken.None));

        Assert.HasCount(2, comparer.Comparisons);
        Assert.AreEqual(comparer.Comparisons[0], comparer.Comparisons[1]);
    }

    [TestMethod]
    public async Task CheckSmbNtlmV1LoginAsync_Refused_WaitsTheRefusalDelayOnTheClock()
    {
        var policy = Create(UserAccount);

        var check = policy.CheckSmbNtlmV1LoginAsync(Login(ntResponse: Flipped(NtResponse, 0)), CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay - TimeSpan.FromMilliseconds(1));
        Assert.IsFalse(check.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.AreEqual(SmbLoginOutcome.Refused, (await check).Outcome);
    }

    [TestMethod]
    public async Task CheckSmbNtlmV1LoginAsync_RefusalDelay_IsCancelledWithTheToken()
    {
        var policy = Create(UserAccount);
        using var cancellation = new CancellationTokenSource();

        var check = policy.CheckSmbNtlmV1LoginAsync(Login(ntResponse: Flipped(NtResponse, 0)), cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(() => check);
    }

    [TestMethod]
    public async Task CheckSmbNtlmV1LoginAsync_CancelledBeforeTheCheck_Throws()
    {
        var policy = Create(UserAccount);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => policy.CheckSmbNtlmV1LoginAsync(Login(), new CancellationToken(true)).AsTask());
    }

    [TestMethod]
    public async Task CheckSmbNtlmV1LoginAsync_NullLogin_Throws()
    {
        var policy = Create(UserAccount);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => policy.CheckSmbNtlmV1LoginAsync(null!, CancellationToken.None).AsTask());
    }

    [TestMethod]
    [DataRow(false, DisplayName = "smb, no TLS")]
    [DataRow(true, DisplayName = "smbs, TLS")]
    public async Task CheckSmbNtlmV1LoginAsync_NtlmV1NotAccepted_IsRefusedUncheckedUndelayedAndUnnoted(bool isTls)
    {
        var everyOtherMethod = new HashSet<AuthenticationMethod>(Enum.GetValues<AuthenticationMethod>());
        everyOtherMethod.Remove(AuthenticationMethod.NtlmV1);
        var policy = Create(UserAccount, everyOtherMethod);

        var check = policy.CheckSmbNtlmV1LoginAsync(Login(tlsSession: isTls ? PolicyFixture.Tls : null), CancellationToken.None);

        Assert.IsTrue(check.IsCompleted);
        Assert.AreEqual(new SmbLoginVerdict(SmbLoginOutcome.Refused, null, null), await check);
    }

    [TestMethod]
    public async Task CheckSmbNtlmV1LoginAsync_DefaultMethods_DoNotAcceptNtlmV1()
    {
        var policy = Create(UserAccount, AuthenticationMethods.DefaultAccepted);

        var verdict = await policy.CheckSmbNtlmV1LoginAsync(Login(), CancellationToken.None);

        Assert.AreEqual(new SmbLoginVerdict(SmbLoginOutcome.Refused, null, null), verdict);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "ntlmv1 accepted")]
    [DataRow(false, DisplayName = "ntlmv1 not accepted")]
    public async Task CheckSmbNtlmV1LoginAsync_AllowAnonymous_IsAcceptedUncheckedWhateverWasSent(bool acceptsNtlmV1)
    {
        var policy = Create(
            PolicyFixture.NoAccounts,
            acceptsNtlmV1 ? AcceptsNtlmV1 : AuthenticationMethods.DefaultAccepted,
            allowAnonymous: true);

        var check = policy.CheckSmbNtlmV1LoginAsync(Login(userName: "nobody", ntResponse: []), CancellationToken.None);

        Assert.IsTrue(check.IsCompleted);
        Assert.AreEqual(new SmbLoginVerdict(SmbLoginOutcome.AcceptedUnchecked, null, null), await check);
    }

    [TestMethod]
    public async Task CheckSmbNtlmV1LoginAsync_NonAsciiPassword_IsCheckedAgainstUpstreamCurlsWidenedUtf8Hash()
    {
        const string password = "pässword";
        var policy = Create(new AccountBook([new Account("User", password)]));
        var widenedUtf8 = NtlmV1Calculation.ComputeResponse(NtlmV1Calculation.ComputeNtHashOfWidenedUtf8(password), ServerChallenge);
        var utf16 = NtlmV1Calculation.ComputeResponse(NtlmV2Calculation.ComputeNtHash(password), ServerChallenge);

        var accepted = await policy.CheckSmbNtlmV1LoginAsync(Login(ntResponse: widenedUtf8), CancellationToken.None);
        var refused = await CheckAsync(policy.CheckSmbNtlmV1LoginAsync(Login(ntResponse: utf16), CancellationToken.None));

        Assert.AreEqual(SmbLoginOutcome.Accepted, accepted.Outcome);
        Assert.AreEqual(SmbLoginOutcome.Refused, refused.Outcome);
    }
}
